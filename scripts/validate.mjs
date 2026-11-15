import { existsSync, mkdirSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { capture, packageId, repository, requireCommit, requireVersion, run, sha256 } from './common.mjs';
import { verifyArtifact } from './verify-artifact.mjs';

const options = new Map();
for (let index = 2; index < process.argv.length; index += 2) {
  const name = process.argv[index];
  const value = process.argv[index + 1];
  if (!['--version', '--output'].includes(name) || !value || options.has(name)) throw new Error('Usage: node scripts/validate.mjs [--version VERSION] [--output DIRECTORY]');
  options.set(name, value);
}
const version = requireVersion(options.get('--version') ?? '1.0.0');
const output = resolve(repository, options.get('--output') ?? 'artifacts');
if (existsSync(output) && readdirSync(output).length) throw new Error('Use an empty artifact output directory to avoid mixing candidates.');
const gitRoot = realpathSync(capture('git', ['rev-parse', '--show-toplevel']));
if (gitRoot !== realpathSync(repository)) throw new Error('Initialize and commit the standalone repository before producing traceable artifacts.');
if (capture('git', ['status', '--porcelain', '--untracked-files=normal'])) throw new Error('Commit all candidate source changes before validation.');
const commit = requireCommit(capture('git', ['rev-parse', 'HEAD']));
run('dotnet', ['restore', 'CardProbability.slnx']);
run('dotnet', ['build', 'CardProbability.slnx', '--configuration', 'Release', '--no-restore', `-p:Version=${version}`, '-p:ContinuousIntegrationBuild=true']);
run('dotnet', ['test', 'tests/CardProbability.EngineTests/CardProbability.EngineTests.csproj', '--configuration', 'Release', '--no-build', '--no-restore']);
for (const sample of ['BasicCalculation', 'SupportedEffects']) {
  run('dotnet', ['run', '--project', `samples/${sample}/${sample}.csproj`, '--configuration', 'Release', '--no-build', '--no-restore']);
}
mkdirSync(output, { recursive: true });
run('dotnet', ['pack', 'src/CardProbability/CardProbability.csproj', '--configuration', 'Release', '--no-build', '--no-restore', `-p:Version=${version}`, `-p:PackageVersion=${version}`, '-p:ContinuousIntegrationBuild=true', '--output', output]);
const artifacts = [`${packageId}.${version}.nupkg`, `${packageId}.${version}.snupkg`];
const manifest = {
  schemaVersion: 1,
  packageId,
  version,
  sourceRepository: 'https://github.com/SeyTi01/card-probability-engine',
  sourceCommit: commit,
  targetFramework: 'net10.0',
  calculatorBaseline: '8d1d7b41086c8bc36b8248bfb5003d70ebd35793',
  sha256: Object.fromEntries(artifacts.map(name => [name, sha256(join(output, name))]))
};
writeFileSync(join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
writeFileSync(join(output, 'SHA256SUMS'), artifacts.map(name => `${manifest.sha256[name]}  ${name}`).join('\n') + '\n');
verifyArtifact(output, version, commit);
console.log(`Verified candidate ${packageId} ${version} from ${commit}; artifact identity saved in ${output}.`);
