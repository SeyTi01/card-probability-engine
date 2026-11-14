# Prepare execution

Inspect the actual OS, writable checkout and tools once; reuse working setup. Read `repo:global.json` and relevant project/workflow files, then check `dotnet --info`, `dotnet --list-sdks` and `node --version` for the selected checks. Use the pinned SDK; do not retarget projects to fit an installed SDK. This library requires no browser/WASM workload for ordinary validation.

Initialize only missing prerequisites in task-writable storage using supported tools and the host's approval mechanism. Use native shell syntax and preserve global configuration. A missing runtime blocks its checks, not source analysis or available publication.

| Observed failure | Procedure |
| --- | --- |
| Git reports dubious ownership | Use command-scoped `-c safe.directory=<repo>`; preserve unrelated configuration. |
| MSBuild reports parallel-node/reuse failure | Retry the failed command with `-m:1`. |
| Sandbox denies required metadata/network/process access | Request narrowly scoped host approval; preserve the exact command/error. |
