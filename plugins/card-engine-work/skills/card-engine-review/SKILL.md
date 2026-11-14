---
name: card-engine-review
description: "Review CardProbability.Engine code or PRs, evaluate review feedback, and implement justified feedback only when requested."
---

# Review the change

Read [context](../../shared/context.md). Inspect the base/head diff, task intent, callers and relevant tests through [affected contracts](../../shared/change-routes.md).

Prioritize actionable numerical, compatibility, ownership, resource and artifact-integrity defects. Give exact locations, a concrete trigger and impact. Separate confirmed findings from hypotheses; do not manufacture defects from preferences.

| Requested work | Procedure |
| --- | --- |
| Execution would resolve a material question | [Verification](../card-engine-verify/SKILL.md) |
| Evaluate review feedback | Check each comment against current source; accept, adapt or explain its mismatch. |
| Fixes are requested | [Implementation](../card-engine-implement/SKILL.md), preserving PR scope |
| Post review/replies to GitHub | [GitHub writing](../../shared/github-writing.md) |

Review-only work ends with findings and material verification limits; it does not require environment setup just to inspect source.
