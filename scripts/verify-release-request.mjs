import { appendFileSync } from 'node:fs';
import { capture, requireCommit, requireVersion } from './common.mjs';

const { CANDIDATE_RUN_ID: runId, PACKAGE_VERSION: rawVersion, SOURCE_COMMIT: rawCommit, PACKAGE_SHA256: hash, REHEARSAL_URL: rehearsal, GITHUB_REPOSITORY: repository, GITHUB_REF: ref } = process.env;
const version = requireVersion(rawVersion);
const commit = requireCommit(rawCommit);
if (repository !== 'SeyTi01/card-probability-engine' || ref !== 'refs/heads/main') throw new Error('Publication is dispatched from the canonical main branch.');
if (!/^\d+$/.test(runId ?? '') || !/^[0-9a-f]{64}$/i.test(hash ?? '')) throw new Error('Candidate run ID and rehearsed package SHA256 are required.');
const evidenceUrl = new URL(rehearsal);
if (evidenceUrl.protocol !== 'https:' || evidenceUrl.hostname !== 'github.com' || !/^\/SeyTi01\/(card-probability-engine|ygo-advanced-probability-calculator-web)\//.test(evidenceUrl.pathname)) throw new Error('Record the calculator rehearsal in one of the public project repositories before publication.');
const candidate = JSON.parse(capture('gh', ['api', `repos/${repository}/actions/runs/${runId}`]));
if (candidate.path !== '.github/workflows/validate.yml' || candidate.event !== 'workflow_dispatch' || candidate.conclusion !== 'success' || candidate.head_sha !== commit || candidate.repository.full_name !== repository) throw new Error('Candidate must be a successful manual validation run of the exact source commit in this repository.');
const tagCommit = capture('git', ['rev-parse', `refs/tags/v${version}^{commit}`]);
if (tagCommit !== commit || capture('git', ['rev-parse', 'HEAD']) !== commit) throw new Error('The checked-out source and release tag must match the candidate commit.');
capture('git', ['merge-base', '--is-ancestor', commit, 'origin/main']);
if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, `Releasing **CardProbability.Engine ${version}** from \`${commit}\`.\n\nCandidate: ${candidate.html_url}\n\nRehearsal: ${evidenceUrl.href}\n\nExpected package SHA256: \`${hash.toLowerCase()}\`\n`);
}
console.log('Candidate run, source revision, release tag, rehearsal record and expected artifact hash are identified.');
