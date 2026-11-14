---
name: card-engine-implement
description: "Implement a CardProbability.Engine feature, fix, refactor, documentation or configuration change, including attached task briefs."
---

# Implement the change

Read [context](../../shared/context.md), the task/issue and relevant current source. Reuse settled decisions; use [planning](../card-engine-plan/SKILL.md) for unresolved multi-step work.

| Source state | Preparation |
| --- | --- |
| New source task | [Start a branch](../../shared/new-branch.md) |
| Continuing an existing task | Preserve its branch and unrelated edits; compare its base with current main. |
| Integration with main is needed | [Rebase](../card-engine-rebase/SKILL.md) |

Read [affected contracts](../../shared/change-routes.md), make the focused change and use [verification](../card-engine-verify/SKILL.md). Update documentation directly affected by the behavior; avoid unrelated rewrites.

| Requested delivery | Finish |
| --- | --- |
| Local-only or an explicit handoff before publication | Return the reviewable local result and verification. |
| Ordinary source implementation | [Deliver the PR](../card-engine-pr/SKILL.md) |
| Called by another workflow | Return the verified changes to that caller; preserve its scope. |
