# Reproduce the legacy exact fractions

[The additional comparison vectors](legacy-exact.json) identify the reference method per fixture. The legacy public Exact captures use the physical solver from engine revision `88d7e83a969bcfa63b2bc84423f4512b50d82e4f`, unlimited Work and Standard Storage. The row order is total, each goal in request order, then each group in request order. Input hashes identify the original Fast worker requests.

Use an isolated checkout of the engine revision above. In the commands below, `$research` is the absolute path of this research directory, `$legacy` points to the isolated engine checkout, and `$outputs` is a directory for generated files. Use .NET SDK 10.0.401.

## Frozen challenges

Copy the opt-in exporter into the legacy test project and run one fixture at a time. The public solver supplied the stored vectors for `challenge-heavy-40-multiplicity`, `challenge-heavy-60-priority` and `challenge-heavy-60-direct`. The six-card challenge has its own offline replay below.

```powershell
Copy-Item "$research/LegacyReferenceReplay.cs" "$legacy/tests/CardProbability.EngineTests/"
$env:CARD_PROBABILITY_REVEAL_CASE = 'challenge-heavy-40-multiplicity'
$env:CARD_PROBABILITY_REVEAL_OUTPUT = $outputs
Push-Location $legacy
dotnet test tests/CardProbability.EngineTests -c Release --filter 'FullyQualifiedName~LegacyReferenceReplay.ExportExactRows'
Pop-Location
```

Accept a captured vector after the test passes. The exporter checks exact accuracy and equal certified endpoints on every row. The nine earlier reference vectors and their original calculator provenance are described in the [research README](README.md).

### Offline six-card reference

The six-card challenge used the original retained-stratum prototype from calculator PR #317. Its diagnostic allowance was 1,500,000 entries and 160,000,000 cells per structure with a frontier capacity of 1,500,000. It exhausted the frontier after 7,650,230 expansions, evicted zero entries and classified exactly the full integer denominator. It used 41,861,910,227 Work and peaked at 1,490,528 entries and 20,294,216 cells. A separate legacy streaming run under Standard Storage was interrupted after the complete reference arrived.

After preparing the worker inputs and calculator assembly described below, build the opt-in engine assembly. The build script takes the original engine checkout and substitutes a local call to the original prototype at the public calculation entry point. Its wrapper requires complete classification and zero evictions before it returns exact fractions. The ordinary engine assembly stays available in the Release configuration.

```powershell
& "$research/original-strata/build.ps1" -LegacyCheckout $legacy
$offlineDll = Join-Path $legacy 'src/CardProbability/bin/OriginalStrata/net10.0/CardProbability.dll'
dotnet run --project "$research/browser/legacy-player" -c Release -- $inputs $hostDll $offlineDll challenge-heavy-40-hand6 $outputs
```

## Player decks

Use a calculator checkout at `a2c9d5ead93ced00353aaf40ac6f6eb8a213e2b0` with the [research host patch](browser/calculator-host.patch) applied. Build that host with the baseline engine package available to its restore. Set `$calculator` to its repository root, and point `$hostDll` to its Release build-output assembly, alongside its normal dependencies. Use the desktop IL build output under `bin/Release/net10.0`. The runner loads a separately supplied legacy engine DLL and invokes the calculator's interactive Exact worker entry point. It checks the completed Exact contract, requires the expected number of rows and preserves the returned integer fractions.

```powershell
$inputs = Join-Path $outputs 'inputs.json'
node "$research/browser/fixtures.cjs" $calculator $inputs
$hostDll = Join-Path $calculator 'YGOProbabilityCalculatorBlazor/bin/Release/net10.0/YGOProbabilityCalculatorBlazor.dll'
$engineDll = Join-Path $legacy 'src/CardProbability/bin/Release/net10.0/CardProbability.dll'
dotnet run --project "$research/browser/legacy-player" -c Release -- $inputs $hostDll $engineDll control-40 $outputs
dotnet run --project "$research/browser/legacy-player" -c Release --no-build -- $inputs $hostDll $engineDll phantom-knights-5 $outputs
```

The control checks the host adapter against an existing six-row reference. Player cases use the source lists and explicitly modeled profiles in the [browser fixture generator](browser/fixtures.cjs). Their goal and group rows describe those authored questions.

The stored Thunder Dragon vectors come from [independent integer enumeration of reveal histories](browser/thunder-reference/README.md). That calculation conditions on the passive cards in the terminal hand and verifies conservation of total outcome mass. The other four player vectors were captured from the legacy public Exact solver through the worker entry point above.
