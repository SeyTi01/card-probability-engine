import assert from 'node:assert/strict';
import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { setTimeout } from 'node:timers/promises';
import { capture, packageId, requireCommit, requireVersion, run, sha256 } from './common.mjs';
import { consumePublishedPackage } from './consume-package.mjs';
import { verifyArtifact } from './verify-artifact.mjs';

const [candidateArg, outputArg] = process.argv.slice(2);
assert(candidateArg && outputArg && process.argv.length === 4,
  'Usage: node scripts/verify-published.mjs <candidate-directory> <evidence-directory>');
const candidate = resolve(candidateArg), output = resolve(outputArg);
const version = requireVersion(process.env.PACKAGE_VERSION);
const commit = requireCommit(process.env.SOURCE_COMMIT);
const runId = process.env.CANDIDATE_RUN_ID;
assert.match(runId ?? '', /^\d+$/);
assert.match(process.env.PACKAGE_SHA256 ?? '', /^[0-9a-f]{64}$/i);
mkdirSync(output, { recursive: true });
const candidateRun = JSON.parse(capture('gh', ['api', `repos/SeyTi01/card-probability-engine/actions/runs/${runId}`]));
assert.equal(candidateRun.repository.full_name, 'SeyTi01/card-probability-engine');
assert.equal(candidateRun.path, '.github/workflows/validate.yml');
assert.equal(candidateRun.event, 'workflow_dispatch');
assert.equal(candidateRun.conclusion, 'success');
assert.equal(candidateRun.head_sha, commit);
assert.equal(capture('git', ['rev-parse', 'HEAD']), commit);
assert.equal(capture('git', ['rev-parse', `refs/tags/v${version}^{commit}`]), commit);
capture('git', ['merge-base', '--is-ancestor', commit, 'origin/main']);
const manifest = verifyArtifact(candidate, version, commit, process.env.PACKAGE_SHA256);
save('candidate-manifest.json', manifest);

const index = await json('https://api.nuget.org/v3/index.json');
const registrationBase = index.resources.find(resource => resource['@type'] === 'RegistrationsBaseUrl/3.6.0')['@id'];
const registrationUrl = `${registrationBase}${packageId.toLowerCase()}/index.json`;
let leaf;
for (let attempt = 0; attempt < 12 && !leaf; attempt++) {
  const registration = await json(registrationUrl);
  for (const page of registration.items) {
    const entries = page.items ?? (await json(page['@id'])).items;
    leaf = entries.find(item => item.catalogEntry.version === version);
    if (leaf) break;
  }
  if (!leaf) {
    console.log(`Waiting for NuGet registration of ${packageId} ${version} (${attempt + 1}/12).`);
    await setTimeout(10000);
  }
}
assert(leaf, 'Published version is not available in NuGet.org registration.');
const metadata = leaf.catalogEntry;
assert.equal(metadata.id, packageId);
assert.equal(metadata.version, version);
assert.notEqual(metadata.listed, false);
assert.equal(metadata.projectUrl, 'https://github.com/SeyTi01/card-probability-engine');
assert(metadata.licenseExpression === 'MIT' || metadata.licenseUrl === 'https://licenses.nuget.org/MIT');
assert.equal(new URL(metadata.readmeUrl).origin, 'https://www.nuget.org');
const readme = await textBody(metadata.readmeUrl);
assert(readme.includes(packageId) && readme.includes('CalculationEngine'), 'NuGet README is missing the package introduction or example.');
const packageUrl = `https://www.nuget.org/packages/${packageId}/${version}`;
const listing = await textBody(packageUrl);
assert(listing.includes(`${packageId} ${version}`), 'NuGet listing must identify the exact version.');
save('registration.json', leaf);

const packageName = `${packageId}.${version}.nupkg`, symbolsName = `${packageId}.${version}.snupkg`;
const publishedPackage = join(output, packageName), publishedSymbols = join(output, symbolsName);
await download(leaf.packageContent, publishedPackage);
const symbolsUrl = `https://www.nuget.org/api/v2/symbolpackage/${packageId}/${version}`;
await download(symbolsUrl, publishedSymbols);
assert.equal(sha256(publishedSymbols), manifest.sha256[symbolsName], 'Published symbols differ from the accepted candidate.');
const signature = capture('dotnet', ['nuget', 'verify', publishedPackage, '--all']);
writeFileSync(join(output, 'signature-verification.txt'), signature + '\n');

// NuGet.org adds a repository signature; every original ZIP payload entry must remain exact.
const payload = JSON.parse(capture('python3', ['-c', `import hashlib, json, sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as original, zipfile.ZipFile(sys.argv[2]) as published:
    before, after = original.namelist(), published.namelist()
    assert len(before) == len(set(before)) and len(after) == len(set(after)), 'Duplicate package ZIP entry'
    assert '.signature.p7s' not in before, 'This check expects an unsigned candidate'
    assert set(after) == set(before) | {'.signature.p7s'}, 'Only the verified repository signature may be added'
    entries = {}
    for name in sorted(before):
        data = original.read(name)
        assert published.read(name) == data, 'Published payload differs: ' + name
        entries[name] = hashlib.sha256(data).hexdigest()
    print(json.dumps(entries))`, join(candidate, packageName), publishedPackage]));
run('dotnet', ['run', '--project', 'tests/PackageInspection/PackageInspection.csproj', '--configuration', 'Release', '--',
  publishedPackage, publishedSymbols, version, commit]);
const restoredPackage = join(output, `restored-${packageName}`);
consumePublishedPackage(version, restoredPackage);
assert.equal(sha256(restoredPackage), sha256(publishedPackage), 'Fresh NuGet.org restore differs from the downloaded public package.');

const verification = {
  schemaVersion: 1, packageId, version, sourceCommit: commit, candidateRun: candidateRun.html_url,
  packageUrl, symbolsUrl, readmeUrl: metadata.readmeUrl, licenseExpression: 'MIT', repository: metadata.projectUrl,
  candidateSha256: manifest.sha256, publishedPackageSha256: sha256(publishedPackage),
  publishedSymbolsSha256: sha256(publishedSymbols), restoredPackageSha256: sha256(restoredPackage),
  repositorySignatureVerified: true, onlyAddedEntry: '.signature.p7s', payloadSha256: payload,
  freshPublicConsumers: ['PublicConsumer', 'ReadmeSample', 'EffectsSample'], passed: true
};
save('nuget-verification.json', verification);
const summary = `# Published package verified\n\n[${packageId} ${version}](${packageUrl}) from \`${commit}\` is listed on NuGet.org with MIT licensing, its README and the expected repository.\n\nAll candidate payload entries are byte-identical after NuGet.org added its verified repository signature. The published symbol package is byte-identical to the candidate, with matching portable PDB and Source Link.\n\nThe public consumer and both samples restored the exact package from NuGet.org into a fresh cache outside the source graph and passed.\n\nPublished package SHA-256: \`${verification.publishedPackageSha256}\`.\n\nPublished symbols SHA-256: \`${verification.publishedSymbolsSha256}\`.\n`;
writeFileSync(join(output, 'summary.md'), summary);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);

async function response(url) {
  for (let attempt = 0; attempt < 12; attempt++) {
    const result = await fetch(url, { headers: { 'Cache-Control': 'no-cache' }, signal: AbortSignal.timeout(30000) });
    if (result.ok) return result;
    if (result.status !== 404) throw new Error(`HTTP ${result.status}: ${url}`);
    console.log(`Waiting for public package resource (${attempt + 1}/12): ${url}`);
    await setTimeout(10000);
  }
  throw new Error(`Public package resource is not available: ${url}`);
}
async function json(url) { return (await response(url)).json(); }
async function textBody(url) { return (await response(url)).text(); }
async function download(url, destination) { writeFileSync(destination, Buffer.from(await (await response(url)).arrayBuffer())); }
function save(name, value) { writeFileSync(join(output, name), JSON.stringify(value, null, 2) + '\n'); }
