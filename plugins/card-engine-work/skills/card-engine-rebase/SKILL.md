---
name: card-engine-rebase
description: "Rebase a CardProbability.Engine task branch onto current main and resolve integration conflicts, including published branches."
---

# Integrate main

Read [context](../../shared/context.md). Confirm the task branch, preserve unrelated edits, fetch `origin/main` and inspect incoming changes. Do not merge main into the task branch.

| Branch state | Preparation |
| --- | --- |
| Already contains current main | No rebase is needed; return to the caller. |
| Unpublished and behind main | Preserve the old head in a backup ref. |
| Published and behind main | Record the exact remote task SHA, reconcile remote commits missing locally, and preserve the old local head in a backup ref. |

Use [local Git identity](../../shared/local-git.md) before rewriting commits. Rebase; resolve each conflict from both sides' intended behavior and [affected contracts](../../shared/change-routes.md), never by wholesale ours/theirs selection. Inspect the integrated diff and rerun invalidated [verification](../card-engine-verify/SKILL.md).

For requested publication of a rewritten remote branch, use [guarded publication](../../shared/rewritten-branch.md). Otherwise return the integrated local branch to the caller.
