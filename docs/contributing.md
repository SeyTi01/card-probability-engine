# Contributing and releases

[Package overview](../README.md) · [Source provenance](provenance.md) · [API guide](generic-calculation-api.md)

## Build and validate

Use the .NET SDK selected by `global.json` and Node.js. Ordinary development needs neither a calculator checkout nor the Blazor/WebAssembly workload.

```sh
dotnet build CardProbability.slnx --configuration Release
dotnet test tests/CardProbability.EngineTests --configuration Release
dotnet run --project samples/BasicCalculation
dotnet run --project samples/SupportedEffects
node scripts/validate.mjs --version 1.0.0 --output artifacts
```

`validate.mjs` builds the library, runs the complete neutral suite and public samples, and packs the Release package once. It inspects the NuGet package and symbols, including TFM, XML documentation, README/license, repository commit, dependency metadata and portable-PDB Source Link. It then restores the public consumer and both samples from that package into fresh temporary projects and a fresh cache outside the source graph. The artifact directory contains the `.nupkg`, `.snupkg`, `manifest.json` and `SHA256SUMS`.

The public consumer must remain public-only. It checks independently counted fractions, distinct-copy collisions, overlapping goals/groups/unions, alternatives, exclusions, non-default hand size, supported effects, activation allowances, snapshot ownership, concurrent reuse, terminal failures and the intended assembly boundary. The library grants friend access only to the exact `CardProbability.EngineTests` assembly. Test frameworks and host application dependencies must never enter the production package.

Source-project references are convenient for development but do not prove package consumption. To recheck existing artifacts without rebuilding:

```sh
node scripts/verify-artifact.mjs artifacts 1.0.0 SOURCE_COMMIT NUPKG_SHA256
```

Replace the final two arguments with the candidate's source commit and expected SHA-256. Preserve independent mathematical expectations when adding regression coverage; deriving expected values from the implementation under test does not validate its semantics.

## Ownership and compatibility

The library owns all source beneath `src/CardProbability`, neutral tests/fixtures, the public API, mathematical model and package consumers. Hosts own domain/catalog interpretation, adapters and policy keys, saved data, presentation, asynchronous execution, worker transport and lifecycle. Do not add host concepts or a broader effect framework merely to ease one integration.

Public names and semantics are both compatibility contracts. Review requirement assignment, effect/tie ordering, `PolicyOrderKey`, defaults, capabilities and accuracy alongside API signatures. Replacing an existing constructor or method with a signature containing a new optional parameter can break already-compiled consumers; retain old signatures when a compatible addition is intended.

Package version, runtime `ModelVersion`, and each host's persistence or transport versions are separate. A package release alone does not imply a model or host-schema change. Stable 1.x releases preserve the documented public API and mathematical semantics.

## Release procedure

1. Choose an unused version and exact source commit. Run the `validate.yml` workflow manually at that commit. It builds and checks one candidate, then uploads its verified artifacts. Record the run ID and manifest/hash identity.
2. Rehearse the actual candidate in the intended host when needed. The initial extraction requires a disposable calculator checkout at the recorded baseline, package substitution with embedded engine inputs removed, native correctness/performance comparison, and production AOT/stripped-IL worker verification. The `rehearse-calculator.yml` workflow compares the exact candidate with the published dependency on a chosen calculator commit and records native and production-worker results. Record source and package hashes with the evidence. Repeat the affected checks whenever source, package or relevant host inputs change.
3. Run `tag-candidate.yml` with the accepted candidate and rehearsal identities to tag the tested source commit as `vVERSION`, or verify an existing matching tag. Confirm the NuGet owner, package ID, unused version, candidate run, expected package SHA-256 and a durable URL for the rehearsal evidence.
4. Run `publish.yml` with `candidate_run_id`, `version`, `source_commit`, `package_sha256` and `rehearsal_url`. The workflow requires a successful manually dispatched candidate at the stated source and a matching tag. It downloads the same artifacts, verifies their identity and public package consumers again, then publishes the package and symbols. It does not rebuild the candidate.
5. Run `verify-published.yml` with the accepted candidate identity to check the NuGet listing, repository signature, unchanged package payload, matching symbols and fresh public-feed consumers. Confirm the repository, version, MIT license, README and installation behavior. Publish concise release notes and attach the package and integration evidence so it survives workflow artifact retention.

Configure NuGet Trusted Publishing for the personal owner and repository `SeyTi01/card-probability-engine`, workflow file **`publish.yml`**, and GitHub environment **`nuget`**. The workflow identifies the public NuGet profile username **`SeyTi`** directly. The release job requests `id-token: write` and uses `NuGet/login@v1` immediately before the push; ordinary PR validation needs no publishing credential. Limit the trust policy to this package and workflow. Forks must configure their own owner/repository policy and username. See [NuGet's setup documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

Treat published versions as durable. Fix a published mistake with a new version; a duplicate-version response does not establish artifact identity. If a process rebuilds the package, the rebuilt artifact must pass the relevant package and integration checks before publication.

For compatible releases after `1.0.0`, add `PackageValidationBaselineVersion` against the published stable version. Binary package validation complements semantic tests; it cannot prove that a new implementation means the same thing.
