# Publish the accepted candidate

Confirm candidate run, source commit, package/symbol hashes, rehearsal URL, unused NuGet version and canonical owner. Verify the source is reachable from main. Create `vVERSION` at that exact tested commit, or verify an existing matching tag. A conflicting tag blocks publication; do not move it.

Dispatch `publish.yml` from main with `candidate_run_id`, `version`, `source_commit`, `package_sha256` and `rehearsal_url`. The workflow downloads and re-verifies the candidate and obtains its short-lived credential through Trusted Publishing; it must not rebuild.

Follow the run to completion. A failure or uncertain push routes to [publication recovery](release-recovery.md). Success routes to [GitHub Release](release-github.md).
