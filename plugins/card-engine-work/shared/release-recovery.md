# Recover an interrupted publication

Inspect actual workflow steps, tag, NuGet package and symbol state before another write. Preserve the accepted candidate and evidence. An existing package requires [published consumption](package-feed.md) and [candidate comparison](published-identity.md).

| Observed state | Action |
| --- | --- |
| No package or symbols were published | Resolve the cause and resume [candidate publication](release-push.md) with the same accepted bytes. |
| Package matches; symbols are missing | Complete only the missing symbol publication through a supported authenticated route using the original `.snupkg`. Do not blindly rerun the package push. |
| Package and symbols match | Continue to [GitHub Release](release-github.md). |
| Any identity conflicts or cannot be established | Stop dependent publication, preserve evidence and report the exact unresolved identity. |

Published versions are durable. An incorrect published package needs a new version through the complete candidate path. A duplicate-version response is not an identity check.
