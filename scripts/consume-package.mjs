import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { isAbsolute, join, relative, sep } from 'node:path';
import { repository, packageId, requireVersion, run } from './common.mjs';

export function consumePackage(packagePath, version) {
  consume(version, { packagePath });
}

export function consumePublishedPackage(version, restoredPackagePath) {
  consume(version, { restoredPackagePath });
}

function consume(version, { packagePath, restoredPackagePath }) {
  requireVersion(version);
  const temporary = realpathSync(mkdtempSync(join(tmpdir(), 'card-probability-package-')));
  const location = relative(repository, temporary);
  if (!location.startsWith(`..${sep}`) && !isAbsolute(location)) throw new Error('Package consumption must run outside the repository.');
  const cache = join(temporary, 'cache');
  if (packagePath) {
    const feed = join(temporary, 'feed');
    mkdirSync(feed);
    cpSync(packagePath, join(feed, `${packageId}.${version}.nupkg`));
  }
  const packageSource = packagePath ? 'feed' : 'https://api.nuget.org/v3/index.json';
  writeFileSync(join(temporary, 'NuGet.Config'), `<configuration><packageSources><clear/><add key="package-source" value="${packageSource}"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>`);
  const env = { ...process.env, NUGET_PACKAGES: cache, NUGET_HTTP_CACHE_PATH: join(temporary, 'http-cache'), DOTNET_CLI_HOME: join(temporary, 'dotnet-home') };
  try {
    for (const [name, source] of [
      ['PublicConsumer', 'tests/PackageConsumer/Program.cs'],
      ['ReadmeSample', 'samples/BasicCalculation/Program.cs'],
      ['EffectsSample', 'samples/SupportedEffects/Program.cs']
    ]) {
      const directory = join(temporary, name);
      mkdirSync(directory);
      cpSync(join(repository, source), join(directory, 'Program.cs'));
      writeFileSync(join(directory, `${name}.csproj`), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>14</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>annotations</Nullable></PropertyGroup><ItemGroup><PackageReference Include="${packageId}" Version="[${version}]"/></ItemGroup></Project>`);
      const project = join(directory, `${name}.csproj`);
      run('dotnet', ['restore', project, '--configfile', join(temporary, 'NuGet.Config'), '--packages', cache, '--no-cache', '-p:NuGetAudit=false', '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false'], { cwd: temporary, env });
      const assets = JSON.parse(readFileSync(join(directory, 'obj/project.assets.json'), 'utf8'));
      const libraries = Object.values(assets.libraries);
      if (libraries.length !== 1 || libraries[0].type !== 'package' || !assets.libraries[`${packageId}/${version}`]) throw new Error('Consumer must resolve exactly the requested package, without project references or transitive dependencies.');
      run('dotnet', ['run', '--project', project, '--configuration', 'Release', '--no-restore', '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false'], { cwd: temporary, env });
    }
    const xml = join(cache, packageId.toLowerCase(), version, 'lib/net10.0/CardProbability.xml');
    if (!existsSync(xml) || !readFileSync(xml, 'utf8').includes('P:CardProbability.CalculationResult.ModelVersion')) throw new Error('Package restore did not deliver the IntelliSense XML.');
    if (restoredPackagePath) cpSync(join(cache, packageId.toLowerCase(), version,
      `${packageId.toLowerCase()}.${version}.nupkg`), restoredPackagePath);
    console.log('Fresh-cache package consumer and both public samples passed outside the source project graph.');
  } finally {
    // The only recursive removal is the verified fresh task-owned temp directory.
    if (temporary.startsWith(realpathSync(tmpdir()) + sep)) rmSync(temporary, { recursive: true, force: true });
  }
}
