# Resource and cancellation contracts

Read `repo:docs/accuracy-and-performance.md` and the affected public policy, assessment and internal budget/cache paths.

Preserve deterministic Work charging, per-structure entry/cell accounting, retained integer payload, cancellation checks and request-local state. Optional cache retention must remain bounded and must not alter counts. Preserve the distinction between primary exhaustion and optional certification exhaustion.

Work is not elapsed time; Storage metrics are not total/live process memory. Assessment is conservative guidance, not validation or a completion guarantee. Preserve the difference between omitted policy and explicit default policy.

Use [measurement guidance](measurements.md) for performance claims or resource profiling.
