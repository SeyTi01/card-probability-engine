---
name: card-engine-verify
description: "Select and run CardProbability.Engine checks, assess regression coverage, or diagnose verification setup."
---

# Verify the affected behavior

Read [context](../../shared/context.md). Select checks from the changed contract or verification question:

| Trigger | Procedure |
| --- | --- |
| Native C# behavior or solution build | [Native checks](../../shared/native-checks.md) |
| Public API, package/build metadata, or release artifact | [Package verification](../card-engine-package/SKILL.md) |
| Test design or coverage review | [Test design](../../shared/testing.md) |
| Performance claim | [Measurements](../../shared/measurements.md) |
| Consuming application or deployed-runtime claim | [Host rehearsal](../../shared/host-rehearsal.md) |
| Plugin instructions or packaging | [Plugin maintenance](../../shared/plugin-maintenance.md) |
| Documentation only | Check claims against current source and resolve links; runtime setup is unnecessary. |

Run focused checks first and reuse evidence for unchanged source/environment. Rerun checks invalidated by edits or integration. Inspect current CI coverage in `repo:.github/workflows/validate.yml`; CI success is not proof of an unexecuted host scenario.

Record actual commands, outcomes and blocked checks in the task conversation. Use [GitHub writing](../../shared/github-writing.md) only when evidence is being posted externally.
