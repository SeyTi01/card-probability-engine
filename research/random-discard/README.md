# Random-discard replay

The exact solver and research workloads were ported from calculator PR #317 at `64e809d`. The original Fast research aimed for intervals at most two percentage points wide on heavy decks. Its best native experiment missed that target. The public engine counts physical outcomes in both Fast and Exact and returns a typed limit if the primary calculation cannot finish. [Original PR](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/pull/317) · [Original research results, proofs and JavaScript prototypes](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/tree/64e809da46e4b139949433c95a7caaabe9c222ee/research/danger-fast)

## Reproduce the exact baseline

The 13 fixtures use neutral IDs. They preserve physical counts, memberships, original policy tie order, activation priorities, hand sizes and goal/group order. Nine have frozen exact references covering every result row. The four heavy challenge variants have no stored exact reference. [Fixture data](../../tests/CardProbability.EngineTests/Fixtures/Data/reveal-cases.json) · [Original inputs and reference provenance](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/blob/64e809da46e4b139949433c95a7caaabe9c222ee/research/danger-fast/fixture-manifest.json)

The regular test suite checks the inexpensive reference cases and the heavy Fast workloads. To run the two heavy Exact replays with unlimited Work and standard Storage, use the following command from the repository root. Allow several minutes per case.

```sh
dotnet test tests/CardProbability.EngineTests -c Release --filter FullyQualifiedName~ReplayHeavyExact --logger "console;verbosity=normal"
```

## Reproduce experimental bounds

The opt-in build adds the original beam, continuation and anonymous-card prototypes, including the whole-batch and retained-hand variants. Use a separate build configuration so normal builds and packages contain only the public solver. Run these commands in PowerShell from the repository root.

```powershell
$researchTargets = (Resolve-Path research/random-discard/Research.targets).Path
dotnet build tests/CardProbability.EngineTests -c RevealResearch -p:Optimize=true "-p:CustomAfterMicrosoftCommonTargets=$researchTargets"
dotnet test tests/CardProbability.EngineTests -c RevealResearch --no-build --filter "FullyQualifiedName~RevealBoundsExperiment&FullyQualifiedName!~ReplayFrozenCase"

$env:CARD_PROBABILITY_REVEAL_CASE = "all"
$env:CARD_PROBABILITY_REVEAL_METHOD = "strata"
$env:CARD_PROBABILITY_REVEAL_WORK = "149000000"
$env:CARD_PROBABILITY_REVEAL_CAPACITY = "2048"
dotnet test tests/CardProbability.EngineTests -c RevealResearch --no-build --filter FullyQualifiedName~ReplayFrozenCase --logger "console;verbosity=normal"
```

Choose a fixture name such as `heavy-40`, `heavy-60` or `challenge-heavy-40-hand6` to replay it individually. The available methods are `beam`, `continuation`, `anonymous`, `batch` and `strata`. The default selects `heavy-40` and `strata` with the Work and capacity shown above. Each result prints integer endpoints for total, goals and groups, a common denominator, charged Work, peak checked storage and the prototype's separate finalization estimate. The runner checks all available frozen fractions by integer cross-multiplication.

For the beam and anonymous methods, a row's lower endpoint is its resolved winning mass. Its upper endpoint adds all unresolved mass. The continuation method instead propagates outward-rounded conditional endpoints. Cancellation and public result reporting stay with the normal engine. The research implementations support the narrower input sets described by their applicability checks.

At 149 million Work and capacity 2,048, the retained-hand experiment reproduced full interval widths of 6.7502 percentage points on `heavy-40`, 2.1381 on `heavy-60` and 18.9000 on `challenge-heavy-40-hand6`. The 40- and 60-card control, single-source and mixed inputs converged to their stored exact fractions.
