import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { packageId, requireCommit, requireVersion, run, sha256 } from './common.mjs';
import { consumePackage } from './consume-package.mjs';

export function verifyArtifact(directory, version, commit, expectedHash) {
  requireVersion(version);
  requireCommit(commit);
  const manifest = JSON.parse(readFileSync(join(directory, 'manifest.json'), 'utf8'));
  if (manifest.schemaVersion !== 1 || manifest.packageId !== packageId || manifest.version !== version || manifest.sourceCommit !== commit) throw new Error('Artifact manifest identity does not match the requested candidate.');
  const packageName = `${packageId}.${version}.nupkg`;
  const symbolsName = `${packageId}.${version}.snupkg`;
  for (const name of [packageName, symbolsName]) {
    if (manifest.sha256[name] !== sha256(join(directory, name))) throw new Error(`Artifact hash mismatch: ${name}`);
  }
  if (expectedHash && manifest.sha256[packageName] !== expectedHash.toLowerCase()) throw new Error('Candidate package does not match the rehearsed SHA256.');
  const packagePath = join(directory, packageName);
  const symbolsPath = join(directory, symbolsName);
  run('dotnet', ['run', '--project', 'tests/PackageInspection/PackageInspection.csproj', '--configuration', 'Release', '--', packagePath, symbolsPath, version, commit]);
  consumePackage(packagePath, version);
  return manifest;
}

if (resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [directory, version, commit, expectedHash] = process.argv.slice(2);
  if (!directory || !version || !commit) throw new Error('Usage: node scripts/verify-artifact.mjs <artifact-directory> <version> <source-commit> [expected-nupkg-sha256]');
  verifyArtifact(resolve(directory), version, commit, expectedHash);
}
