# Performance evidence

Compare baseline and candidate with identical fixtures, host/runtime, build mode, policies, warmup and harness. Record revisions, raw outcomes and first versus warm calls. Run timings without concurrent builds, tests or profiles.

Verify fractions, proof/status and model scope before interpreting speed. Keep instrumentation separate from uninstrumented timings. Timeout samples are censored observations, not completed results or intrinsic Work/Storage limits. Allocations, sampled heap, working set and checked structure peaks measure different things.

| Measurement requested | Procedure |
| --- | --- |
| Storage or ordered-state diagnostics | [Neutral profiles](profiles.md) |
| Effect on an actual consuming application/runtime | [Host rehearsal](host-rehearsal.md) |

Keep generated evidence in separate ignored directories. Report uncertainty and regressions; native evidence does not establish browser AOT behavior.
