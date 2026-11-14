# Establish the hosted candidate

Read `repo:docs/contributing.md`, `scripts/verify-release-request.mjs` and both release workflows. Choose an exact commit reachable from main and an unused version; inspect existing matching manual candidates before dispatching.

Dispatch `validate.yml` with the intended version using a ref resolving to that commit. Wait for completion and verify repository, workflow path, `workflow_dispatch` event, successful conclusion and exact head SHA. A PR/push validation run is not a release candidate.

Download its retained artifacts, record run ID, manifest and package/symbol hashes, and use [artifact verification](package-verify.md). Keep the candidate unchanged.

Publication requires a durable rehearsal URL accepted by the current guard. Reuse evidence only for the same candidate and unchanged relevant host inputs. Missing or invalidated host evidence routes to [host rehearsal](host-rehearsal.md); do not fabricate a record or weaken the workflow.

Record the version, source commit, run ID, hashes and rehearsal URL together for publication/resume.
