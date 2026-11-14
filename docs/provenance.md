# Source provenance

The initial library transfer comes from [SeyTi01/ygo-advanced-probability-calculator-web](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web) at commit [`8d1d7b41086c8bc36b8248bfb5003d70ebd35793`](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/commit/8d1d7b41086c8bc36b8248bfb5003d70ebd35793). That exact commit is also the embedded comparison baseline for the initial calculator rehearsal.

The public API prerequisite, [calculator issue #367](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/issues/367), was completed before this transfer in commit [`510a153`](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/commit/510a153). The later baseline also includes opt-in storage profiling and larger weighted-search memoization limits. Those changes and their tests are included rather than reverting to the earlier boundary-audit revision.

## Transfer inventory

| Origin | Destination | Transfer |
| --- | --- | --- |
| `YGOProbabilityCalculatorBlazor/Engine/**/*.cs` | `src/CardProbability/**/*.cs` | All 73 C# files, identical Git contents. |
| `CardProbability.EngineTests/**/*.cs` | `tests/CardProbability.EngineTests/**/*.cs` | All 44 C# files, identical Git contents. |
| `CardProbability.EngineTests/Fixtures/Data/*.json` | `tests/CardProbability.EngineTests/Fixtures/Data/*.json` | All four fixtures, identical Git contents. |
| Neutral test project | `tests/CardProbability.EngineTests/CardProbability.EngineTests.csproj` | Adapted project reference/build wiring; fixture copying and friend assembly identity preserved. |
| `scripts/engine-boundary/Consumer.cs` | `tests/PackageConsumer/Program.cs` | Public fixture source adapted to actual package consumption. |
| Generic API and model documentation | `docs/` and `README.md` | Adapted to the independent library with neutral terminology and local source links. |
| `LICENSE` | `LICENSE` | Original MIT notice and attribution preserved. |

[transfer-manifest.json](transfer-manifest.json) lists every engine and neutral-test source/fixture/project path and the SHA-256 of its exact originating Git blob. The project file is the only listed file intentionally adapted during initial source transfer; source/test C# and JSON Git contents must match their hashes. A Windows checkout may use CRLF while Git stores LF; compare committed blobs or normalize checkout line endings before checking these hashes. Build products (`bin`, `obj`, `TestResults`) are excluded.

The engine uses `net10.0`, C# 14, implicit usings and nullable annotations. It has no external runtime package, generated-input, conditional-symbol, embedded-resource, native-interop or application-assembly-name dependency. Its only friend declaration is the exact `CardProbability.EngineTests` assembly. The production project explicitly names the assembly `CardProbability`; public/internal namespaces remain `CardProbability` and `CardProbability.Internal`.

The SDK's Git source mapping and portable PDB refer to the standalone source commit from which each package was built. A candidate's `manifest.json` and `SHA256SUMS` identify that package and symbol artifact; the original calculator hashes establish transfer provenance rather than replacing the package's own source identity.

## Integration boundary

The initial extraction preserves solver behavior. The calculator temporarily substitutes the candidate package for verification; permanent adoption belongs to [issue #368](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/issues/368). Its adapters, domain-derived `PolicyOrderKey` values, saved inputs, catalogs, localization, UI, worker exports, Progressive coordination, cancellation ownership and stale-result protection remain host-owned.

The calculator's engine and neutral-test source remain embedded until that adoption is completed. Any subsequent engine change in the calculator must be reconciled into this library before the embedded copy is removed. Catalog data, translations and artwork are outside this MIT source transfer.
