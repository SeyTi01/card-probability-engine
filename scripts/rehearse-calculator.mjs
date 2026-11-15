import assert from 'node:assert/strict';
import { appendFileSync, cpSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { capture, packageId, requireCommit, requireVersion, run, sha256 } from './common.mjs';
import { verifyArtifact } from './verify-artifact.mjs';

const [packageArg, baselineArg, candidateArg, evidenceArg] = process.argv.slice(2);
assert(packageArg && baselineArg && candidateArg && evidenceArg && process.argv.length === 6,
  'Usage: node scripts/rehearse-calculator.mjs <candidate-package> <baseline-host> <candidate-host> <evidence>');
const packageDirectory = resolve(packageArg), evidence = resolve(evidenceArg);
const version = requireVersion(process.env.PACKAGE_VERSION);
const sourceCommit = requireCommit(process.env.SOURCE_COMMIT);
const hostCommit = requireCommit(process.env.HOST_COMMIT);
const runId = process.env.CANDIDATE_RUN_ID;
assert.match(runId ?? '', /^\d+$/);
assert.match(process.env.PACKAGE_SHA256 ?? '', /^[0-9a-f]{64}$/i);
mkdirSync(evidence, { recursive: true });

const candidateRun = JSON.parse(capture('gh', ['api', `repos/SeyTi01/card-probability-engine/actions/runs/${runId}`]));
assert.equal(candidateRun.repository.full_name, 'SeyTi01/card-probability-engine');
assert.equal(candidateRun.path, '.github/workflows/validate.yml');
assert.equal(candidateRun.event, 'workflow_dispatch');
assert.equal(candidateRun.conclusion, 'success');
assert.equal(candidateRun.head_sha, sourceCommit);
assert.equal(capture('git', ['rev-parse', 'HEAD']), sourceCommit);
capture('git', ['merge-base', '--is-ancestor', sourceCommit, 'origin/main']);
const manifest = verifyArtifact(packageDirectory, version, sourceCommit, process.env.PACKAGE_SHA256);
cpSync(join(packageDirectory, 'manifest.json'), join(evidence, 'candidate-manifest.json'));
cpSync(join(packageDirectory, 'SHA256SUMS'), join(evidence, 'candidate-SHA256SUMS'));

// Reuse the frozen public-wire corpus, never regenerate expected results on the candidate.
const fixtureHash = '4c2d7e5c94dc8c0bd44e62a6ef6752b08f4d1d7d4879c78e8c274a68a2990ec6';
const archive = join(evidence, 'preview-rehearsal.zip');
run('curl', ['--fail', '--location', '--retry', '3', '--output', archive,
  'https://github.com/SeyTi01/card-probability-engine/releases/download/v1.0.0-preview.1/calculator-rehearsal-1.0.0-preview.1.zip']);
assert.equal(sha256(archive), '5d927104e7bcf0273fc3d088fa1e89fbe2a351ea77ed68da3d199eccdb8e4034');
const archived = join(evidence, 'preview-rehearsal');
run('unzip', ['-q', archive, '-d', archived]);
const archivedFixture = join(archived, 'frozen-comparison.json');
assert.equal(sha256(archivedFixture), fixtureHash);
const fixture = join(evidence, 'frozen-fixtures.json');
cpSync(archivedFixture, fixture);
assert.equal(JSON.parse(readFileSync(fixture)).length, 16);

const project = 'YGOProbabilityCalculatorBlazor/YGOProbabilityCalculatorBlazor.csproj';
const solution = 'YGOProbabilityCalculatorBlazor.sln';
const engineReference = /(<PackageReference Include="CardProbability\.Engine" Version=")([^"]+)("\s*\/>)/;
const hosts = [
  { name: 'baseline', directory: resolve(baselineArg) },
  { name: 'candidate', directory: resolve(candidateArg) }
];
const identity = {
  schemaVersion: 1, version, sourceCommit, hostCommit, candidateRun: candidateRun.html_url,
  workflowRun: `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`,
  packageHashes: manifest.sha256, fixtureSha256: fixtureHash, sdk: '10.0.402', hosts: [], passed: false
};
save('identity.json', identity);

for (const host of hosts) {
  assert.equal(capture('git', ['rev-parse', 'HEAD'], { cwd: host.directory }), hostCommit);
  assert.equal(capture('git', ['status', '--porcelain'], { cwd: host.directory }), '');
  const projectPath = join(host.directory, project);
  const original = readFileSync(projectPath, 'utf8');
  const reference = original.match(engineReference);
  assert(reference, 'The host must already use a pinned package reference.');
  host.version = host.name === 'candidate' ? version : requireVersion(reference[2]);
  if (host.name === 'candidate') writeFileSync(projectPath, original.replace(engineReference,
    (_match, before, _oldVersion, after) => before + version + after));
  writeFileSync(join(host.directory, 'global.json'), JSON.stringify({ sdk: { version: '10.0.402', rollForward: 'disable' } }));
  const scratch = join(process.env.RUNNER_TEMP, 'calculator-rehearsal', host.name);
  mkdirSync(scratch, { recursive: true });
  host.env = { ...process.env, NUGET_PACKAGES: join(scratch, 'packages'), NUGET_HTTP_CACHE_PATH: join(scratch, 'http-cache') };
  host.command = (command, args, env = {}) => run(command, args, { cwd: host.directory, env: { ...host.env, ...env } });
  const feed = host.name === 'candidate'
    ? `<add key="candidate" value="${packageDirectory}"/>` : '';
  const mapping = host.name === 'candidate'
    ? '<packageSource key="candidate"><package pattern="CardProbability.Engine"/></packageSource>' : '';
  const config = join(scratch, 'NuGet.Config');
  writeFileSync(config, `<configuration><packageSources><clear/>${feed}<add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><packageSourceMapping><clear/>${mapping}<packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>`);
  host.command('node', ['--test', 'scripts/engine-boundary/guards.test.mjs']);
  host.command('node', ['scripts/engine-boundary/check.mjs', '--guards-only']);
  host.command('dotnet', ['restore', solution, '--configfile', config, '--no-cache']);
  const assets = JSON.parse(readFileSync(join(host.directory, 'YGOProbabilityCalculatorBlazor/obj/project.assets.json')));
  const engineLibraries = Object.keys(assets.libraries).filter(key => key.startsWith(`${packageId}/`));
  assert.deepEqual(engineLibraries, [`${packageId}/${host.version}`]);
  assert.equal(assets.libraries[engineLibraries[0]].type, 'package');
  const cachedPackage = join(host.env.NUGET_PACKAGES, packageId.toLowerCase(), host.version,
    `${packageId.toLowerCase()}.${host.version}.nupkg`);
  if (host.name === 'candidate') assert.equal(sha256(cachedPackage), manifest.sha256[`${packageId}.${version}.nupkg`]);
  host.command('dotnet', ['build', solution, '-c', 'Release', '--no-restore']);
  const cachedDll = join(host.env.NUGET_PACKAGES, packageId.toLowerCase(), host.version, 'lib/net10.0/CardProbability.dll');
  const testDll = join(host.directory, 'YGOProbabilityCalculatorBlazorTest/bin/Release/net10.0/CardProbability.dll');
  assert.equal(sha256(testDll), sha256(cachedDll));
  const properties = JSON.parse(capture('dotnet', ['msbuild', project, '-p:Configuration=Release',
    '-getProperty:RunAOTCompilation,WasmStripILAfterAOT'], { cwd: host.directory, env: host.env })).Properties;
  assert.equal(properties.RunAOTCompilation, 'true');
  assert.equal(properties.WasmStripILAfterAOT, 'true');
  host.wwwroot = join(scratch, 'publish/wwwroot');
  host.command('dotnet', ['publish', project, '-c', 'Release', '--no-restore', '--output', join(scratch, 'publish')]);
  identity.hosts.push({ name: host.name, packageVersion: host.version, restoredPackageSha256: sha256(cachedPackage),
    engineAssemblySha256: sha256(testDll) });
  save('identity.json', identity);
}

const candidate = hosts[1];
candidate.command('dotnet', ['test', solution, '-c', 'Release', '--no-build', '--no-restore']);
candidate.command('node', ['--test']);

// Both builds and correctness jobs are complete before paired measurements begin.
for (const host of hosts) {
  host.command('dotnet', ['test', solution, '-c', 'Release', '--no-build', '--no-restore', '--filter',
    'FullyQualifiedName=YGOProbabilityCalculatorBlazorTest.Services.ProbabilityCalculator.PerformanceAuditBenchmark.Replay'], {
    DOTNET_TieredCompilation: '0', YGO_AUDIT_INPUT: fixture, YGO_AUDIT_OUTPUT: join(evidence, `${host.name}-native.json`), YGO_AUDIT_TRIALS: '5'
  });
  host.command('node', ['scripts/background-calculation/measure-worker.cjs', host.wwwroot,
    fixture, join(evidence, `${host.name}-worker.json`), '5', '60000']);
}
candidate.command('node', ['scripts/background-calculation/integration-smoke.cjs', candidate.wwwroot, join(evidence, 'integration.json')]);
candidate.command('node', ['scripts/background-calculation/lifecycle-smoke.cjs', candidate.wwwroot]);
for (const host of hosts) host.command('node', [join(archived, 'cpu-cancellation.cjs'), host.directory,
  host.wwwroot, join(archived, 'frozen-controls.json'), join(archived, 'cpu-cancellation-request.json'),
  join(evidence, `${host.name}-cpu-cancellation.json`)]);

const comparison = ['native', 'worker'].flatMap(runtime => {
  const baseline = JSON.parse(readFileSync(join(evidence, `baseline-${runtime}.json`)));
  const current = JSON.parse(readFileSync(join(evidence, `candidate-${runtime}.json`)));
  assert.equal(baseline.FixtureSha256, fixtureHash);
  assert.equal(current.FixtureSha256, fixtureHash);
  assert.equal(current.Measurements.length, baseline.Measurements.length);
  return baseline.Measurements.map((before, index) => {
    const after = current.Measurements[index];
    assert.equal(after.Name, before.Name);
    assert.equal(after.JsonSha256, before.JsonSha256);
    for (const measurement of [before, after]) assert(measurement.Samples.every(sample => sample.Parity === true));
    const warm = measurement => measurement.Samples.filter(sample => runtime === 'native' ? sample.Phase === 'warm' : !sample.Cold)
      .map(sample => sample.Milliseconds).sort((a, b) => a - b);
    const beforeWarm = warm(before), afterWarm = warm(after);
    return { runtime, name: before.Name, baselineMedianMs: median(beforeWarm), candidateMedianMs: median(afterWarm),
      baselineFirstMs: before.Samples[0].Milliseconds, candidateFirstMs: after.Samples[0].Milliseconds };
  });
});
save('comparison.json', comparison);
identity.passed = true;
save('identity.json', identity);
const rows = comparison.map(row => `| ${row.runtime} | ${row.name} | ${display(row.baselineMedianMs)} | ${display(row.candidateMedianMs)} |`).join('\n');
const summary = `# Calculator candidate rehearsal\n\nPackage **${packageId} ${version}**, source \`${sourceCommit}\`.\n\nHost: \`${hostCommit}\`; [candidate run](${candidateRun.html_url}).\n\nPackage SHA-256: \`${manifest.sha256[`${packageId}.${version}.nupkg`]}\`.\n\nExact candidate public consumers, calculator package guards, application/JavaScript tests, frozen native and production AOT/stripped-IL worker replay, real-worker integration and idle lifecycle checks passed. Both hosts replayed the same 16 frozen cases and passed the separate CPU-bound cancellation probe with physical worker closure and successful recreation.\n\n| Runtime | Fixture | Baseline warm median (ms) | Candidate warm median (ms) |\n| --- | --- | ---: | ---: |\n${rows}\n\nFive warm samples per successful case; typed failures have first observations only. Review timing differences before publication; these measurements establish neither cold-network parity nor cross-device performance. Full post-publication NuGet.org consumption remains a separate check.\n`;
writeFileSync(join(evidence, 'summary.md'), summary);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);

function save(name, value) { writeFileSync(join(evidence, name), JSON.stringify(value, null, 2) + '\n'); }
function median(values) { return values.length ? values[Math.floor(values.length / 2)] : null; }
function display(value) { return value === null ? '—' : value.toFixed(3); }
