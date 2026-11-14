# Prepare dirty candidate source

Inspect every dirty/untracked path and separate task changes from pre-existing edits. Finish the requested source preparation through [implementation](../skills/card-engine-implement/SKILL.md), retaining the caller's delivery limits.

Commit only reviewed task changes using [local Git identity](local-git.md). Use an isolated checkout for a clean candidate when unrelated edits must remain. Never bypass the clean-source guard or label a dirty build with a committed-source identity.
