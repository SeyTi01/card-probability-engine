# Deliver source

Inspect the complete diff, requested scope and current `origin/main`. Use [rebase](../skills/card-engine-rebase/SKILL.md) when main has advanced beyond the branch base, then rerun invalidated [verification](../skills/card-engine-verify/SKILL.md).

Use [publication](git-publication.md). Combine only unpushed refinements of one logical change; keep scratch files and generated artifacts out of commits. Verify the remote commit/tree matches the reviewed result.

Create/update the focused PR against `main`, link its issue and describe the final behavior using [GitHub writing](github-writing.md). Verify base, head and changed files. Return the open PR to the caller.
