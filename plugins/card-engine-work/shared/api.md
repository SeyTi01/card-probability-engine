# Public API compatibility

Read `repo:docs/generic-calculation-api.md`, the ownership/compatibility section of `repo:docs/contributing.md`, and the affected public declarations/callers.

Review semantic and binary compatibility together: IDs, bounds, order and policy keys, defaults, immutable snapshots, concurrent request reuse, cancellation and terminal result shape. Retain an old constructor/method signature when adding compatible options; replacing it with an extra optional parameter can break compiled callers.

Keep package ID, assembly/namespace, package SemVer and runtime `ModelVersion` distinct. Choose versions from the actual API/model change; do not bump model metadata for an ordinary package release. Preserve XML API documentation and runtime model metadata rather than inlining it in consumers.

Keep implementation details internal. Only the exact `CardProbability.EngineTests` assembly receives friend access; samples, hosts and `PackageConsumer` use the public API.
