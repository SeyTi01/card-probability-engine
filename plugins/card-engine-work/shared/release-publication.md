# Complete publication

Read `repo:docs/contributing.md`, current workflows and `scripts/verify-release-request.mjs`. Establish available GitHub workflow/tag/release access and NuGet Trusted Publishing configuration without exposing credentials. The configured owner, workflow and environment come from source and live settings, not inherited calculator assumptions.

Inspect requested tag/release, NuGet version, existing PRs/candidates and any interrupted publish run. Keep a compact uncommitted record of version, source commit, candidate run/hashes, rehearsal URL and completed writes. Reconcile it with remote state.

| Current state | Next procedure |
| --- | --- |
| Source/version changes remain | [Prepare source](release-source.md); merge its reviewed PR only after required checks pass, then resolve the final source commit. |
| No accepted hosted candidate | [Candidate workflow](release-candidate.md) |
| Accepted candidate with matching evidence | [Publish candidate](release-push.md) |
| Package exists or a push outcome is uncertain | [Publication recovery](release-recovery.md) |
| Package and symbols are verified published | [GitHub Release](release-github.md) |

Use the furthest verified stage. Do not rebuild accepted bytes, retag later main, overwrite a published version, bypass protections, or roll back a newer release's latest marker.
