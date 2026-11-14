---
name: card-engine-diagnose
description: "Investigate numerical errors, terminal failures, resource limits or performance regressions in CardProbability.Engine."
---

# Diagnose the behavior

Read [context](../../shared/context.md). Establish expected/observed behavior, input, mode, policies, revisions and host. Inspect the smallest relevant path using [affected contracts](../../shared/change-routes.md).

Reproduce at the simplest reliable layer through [verification](../card-engine-verify/SKILL.md). Distinguish engine failure from invalid fixtures, harness or environment failures. Support causation with evidence, and retain uncertainty.

| Investigation | Procedure |
| --- | --- |
| Numerical mismatch | [Independent expectations](../../shared/testing.md) |
| Performance or resource profile | [Measurements](../../shared/measurements.md) |
| Repair was requested | Continue through [implementation](../card-engine-implement/SKILL.md). |

Diagnosis-only work ends with the supported cause, reproduction and limits. Do not broaden into an unrelated optimization campaign.
