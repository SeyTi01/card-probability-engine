import assert from 'node:assert/strict';
import { appendFileSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { capture, requireCommit, requireVersion } from './common.mjs';
import { verifyArtifact } from './verify-artifact.mjs';

const repository = 'SeyTi01/card-probability-engine';
assert.equal(process.env.GITHUB_REPOSITORY, repository);
assert.equal(process.env.GITHUB_REF, 'refs/heads/main');
const [candidateArg, rehearsalArg] = process.argv.slice(2);
assert(candidateArg && rehearsalArg && process.argv.length === 4,
  'Usage: node scripts/tag-candidate.mjs <candidate-directory> <rehearsal-directory>');
const version = requireVersion(process.env.PACKAGE_VERSION);
const commit = requireCommit(process.env.SOURCE_COMMIT);
const candidateId = process.env.CANDIDATE_RUN_ID, rehearsalId = process.env.REHEARSAL_RUN_ID;
assert.match(candidateId ?? '', /^\d+$/);
assert.match(rehearsalId ?? '', /^\d+$/);
assert.match(process.env.PACKAGE_SHA256 ?? '', /^[0-9a-f]{64}$/i);
const candidate = verifiedRun(candidateId, '.github/workflows/validate.yml');
const rehearsal = verifiedRun(rehearsalId, '.github/workflows/rehearse-calculator.yml');
assert.equal(rehearsal.head_branch, 'main');
assert.equal(candidate.head_sha, commit);
assert.equal(capture('git', ['rev-parse', 'HEAD']), commit);
capture('git', ['merge-base', '--is-ancestor', commit, 'origin/main']);
const manifest = verifyArtifact(resolve(candidateArg), version, commit, process.env.PACKAGE_SHA256);
const evidence = JSON.parse(readFileSync(join(resolve(rehearsalArg), 'identity.json'), 'utf8'));
assert.equal(evidence.schemaVersion, 1);
assert.equal(evidence.passed, true);
assert.equal(evidence.version, version);
assert.equal(evidence.sourceCommit, commit);
assert.equal(evidence.candidateRun, candidate.html_url);
assert.equal(evidence.workflowRun, rehearsal.html_url);
assert.deepEqual(evidence.packageHashes, manifest.sha256);

const tagName = `v${version}`, refName = `refs/tags/${tagName}`;
const existing = api(`git/matching-refs/tags/${tagName}`).find(ref => ref.ref === refName);
if (existing) assert.equal(tagCommit(existing), commit, 'An existing release tag points to a different commit; it must not be moved.');
else api('git/refs', ['--method', 'POST', '-f', `ref=${refName}`, '-f', `sha=${commit}`]);
assert.equal(tagCommit(api(`git/ref/tags/${tagName}`)), commit);
const summary = `${existing ? 'Reused' : 'Created'} **${tagName}** at \`${commit}\`.\n\nCandidate: ${candidate.html_url}\n\nCalculator rehearsal: ${rehearsal.html_url}\n\nThe accepted package and rehearsal hashes match. This workflow publishes neither a NuGet package nor a GitHub Release.\n`;
console.log(summary);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);

function api(path, args = []) { return JSON.parse(capture('gh', ['api', `repos/${repository}/${path}`, ...args])); }
function verifiedRun(id, path) {
  const run = api(`actions/runs/${id}`);
  assert.equal(run.repository.full_name, repository);
  assert.equal(run.path, path);
  assert.equal(run.event, 'workflow_dispatch');
  assert.equal(run.conclusion, 'success');
  return run;
}
function tagCommit(ref) {
  let object = ref.object;
  while (object.type === 'tag') object = api(`git/tags/${object.sha}`).object;
  assert.equal(object.type, 'commit');
  return object.sha;
}
