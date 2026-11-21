# Published calculator rehearsal

The harness imports a saved session, clicks Calculate and observes the result in the published Blazor desktop UI. It times the captured click through the first animation frame where the button is idle and the current result rows have visible values. It records each row's label, percentage, uncertainty and bound primary fraction, and checks these against the production worker response. An animation frame establishes render readiness. Physical display presentation time is outside this measurement.

## Inputs and model

The 13 frozen inputs come directly from calculator PR #317. The fixture converter validates their canonical SHA-256 digests before generating sessions. After session upload the calculator may add catalog-owned name, mention and archetype tags. The harness omits those unreferenced tags from its input comparison and equates two encodings of fixed bounds and encounter order. All other worker input fields must match, including effect identities, card and goal order, fixed maxima, hand size, priority, 150,000,000 Work and Standard Storage. [Pinned source inputs](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web/blob/64e809da46e4b139949433c95a7caaabe9c222ee/research/danger-fast/frozen-fixtures.json)

Three historical player lists retain all original main-deck entries and copy counts. The benchmark supplies the hand goals and models base reveals plus the indicated draw or search effect. Other card effects, discard triggers, summons, the Extra Deck and the Side Deck are outside these hand-goal models. Each list runs at both five and six cards with the same requirements and encounter-order policy.

| Deck | Copies with base reveal | Other modeled effect | Post-effect hand goals |
| --- | ---: | --- | --- |
| 40-card Phantom Knights | 2 | Reinforcement of the Army searches a level-four-or-lower Warrior | Tour Guide, or a level-three Phantom Knight plus a selected level-three extender. [Player list](https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=1fb7b0d92c8a06e7454799c3954b4f1b&dno=1&request_locale=en) |
| 40-card Thunder Dragon | 10 | Allure draws two and removes a DARK card | A Thunder Dragon monster plus either Batteryman Solar or Thunder Dragon Fusion. [Player list](https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=86799636d2786f271b387fa5936cb2f1&dno=4&request_locale=en) |
| 60-card Lightsworn | 4 | Pot of Desires removes ten unseen cards and draws two | Charge of the Light Brigade, or Solar Recharge plus a Lightsworn monster. [Player list](https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=a521ae77a86116352a29653983f1fd1d&dno=1&request_locale=en) |

Each player session displays both individual goal rows, their group union and the total. [Main-deck counts and goal memberships](player-decks.json)

## Run

Use a disposable checkout of the calculator at `a2c9d5ead93ced00353aaf40ac6f6eb8a213e2b0`. Install the .NET 10 SDK and its wasm-tools workload. From the engine repository, set `$calculator`, `$localFeed` and `$published` to absolute paths.

```powershell
$patch = (Resolve-Path research/random-discard/browser/calculator-host.patch).Path
git -C $calculator apply $patch
git -C $calculator fetch origin refs/pull/317/head
$project = Join-Path $calculator YGOProbabilityCalculatorBlazor/YGOProbabilityCalculatorBlazor.csproj
$version = ([xml](Get-Content -Raw $project)).Project.ItemGroup.PackageReference |
  Where-Object Include -eq 'CardProbability.Engine' | Select-Object -ExpandProperty Version
dotnet pack src/CardProbability/CardProbability.csproj -c Release "-p:Version=$version" -o $localFeed
$env:NUGET_PACKAGES = Join-Path $calculator obj/rehearsal-packages
dotnet restore (Join-Path $calculator YGOProbabilityCalculatorBlazor.sln) "-p:RestoreAdditionalProjectSources=$localFeed"
dotnet publish $project -c Release --no-restore -o $published
$publishedWwwroot = Join-Path $published wwwroot
$restoredNupkg = Join-Path $localFeed "CardProbability.Engine.$version.nupkg"
```

Use a separate empty feed, package cache and publish output for each candidate. The checkout uses the pinned PR #317 commit to reconstruct inputs. Use the calculator's browser setup for external Playwright and a Chromium-based browser. Keep other tests, builds and reference calculations paused throughout a timing run.

```powershell
$env:NODE_PATH = 'C:\tools\node_modules'
$env:YGO_BROWSER_CHANNEL = 'msedge'
node research/random-discard/browser/calculate.cjs $calculator $publishedWwwroot $newReport $restoredNupkg
node research/random-discard/browser/verify-references.cjs $newReport research/random-discard/legacy-exact.json
node research/random-discard/browser/default-flow.cjs $calculator $publishedWwwroot $newDefaultFlowReport
node research/random-discard/browser/table.cjs $newReport $newTable
```

The default run covers all 19 cases. Set `REVEAL_CASES` to a comma-separated list of exact fixture names for a subset. Set `REVEAL_WARM` to change the default three repeated clicks. `REVEAL_CUTOFF_MS` defaults to 60,000. The candidate must expose actual engine Work and peak Storage counters on each success. When reproducing the historical host without these counters set `REVEAL_REQUIRE_METRICS=0`. Use the same timing settings for comparisons. `summarize.cjs <raw-report> <new-summary>` preserves each distinct result, every sample and available engine Work/Storage counters. [PR #8 baseline on desktop Edge](edge-baseline.json)

The recorded baseline and candidate used Windows x64, .NET SDK 10.0.401, browser runtime 10.0.12, Release AOT and stripped IL. The original baseline host preceded the metrics envelope and translated language-resource additions to the rehearsal patch. Both hosts use the same calculator UI, worker owner and settings. Their distinct published-asset and package hashes are preserved in the reports.

Each fixture starts in a fresh browser context after the page and session have loaded. Its first click includes production worker bootstrap. Subsequent clicks must use that same worker. The timeout is a censored observation. A Work or Storage terminal records the calculator's visible error and has no result or warm sample. The harness records typed failures and continues to the remaining cases. An existing report path is refused so trials retain their identities. A nonzero exit indicates a failed check, missing result or missed target.

To exercise optional interactive Exact, use `REVEAL_MODE=Exact` and select the intended heavy cases with `REVEAL_CASES`. The harness first warms the worker with a regular FastOnly click on the same deck, opens the calculator's settings and selects Exact. It measures the next click on the same worker and verifies the interactive Exact entry point. Every successful row must have zero certified width. Standard Storage remains active. Exact has unlimited Work, a default 60-second observation cutoff and one measured warm click.

The separate untimed default-flow check verifies the fresh app's selected calculation mode, then imports a session saved with that default Progressive strategy. It opens Help, returns to the imported deck, clicks Calculate, and verifies the ordinary exact result and the heavy result through the default Progressive owner. When a bounded result starts a physical upgrade, the check uses the visible cancellation control and verifies the accepted rows remain available.

Pass the optional Exact and default-flow reports as the third and fourth arguments to `table.cjs` to include these checks. The table generator confirms every available physical Exact fraction falls inside each Fast sample's certified interval. [Measured player goals and desktop results](results.md)

To verify stored numerical results offline from the engine root, run `node research/random-discard/browser/verify-references.cjs research/random-discard/browser/edge-final.json`. The checker accepts raw or compact reports and reads the original frozen fractions plus the additional exact reference vectors. Running it on `edge-final-exact.json` lists any cases without a terminal row, including the censored six-card Thunder Dragon sample.

Eligibility is checked on every warm sample. Up to two reveal copies require exact rows and at most 1,000 ms. Larger counts require exact rows or certified full widths of at most two percentage points on every row and at most 5,000 ms. After the displayed center and radius are rounded, each heavy row must still show at most ±1.00 percentage point. Each available Exact reference is checked using integer cross-multiplication. Store raw reports in a local ignored artifacts directory. The reports include the calculator revision, engine package hash, published framework/worker hash, CPU model and browser build. To export the exact expected host inputs for an independent replay, run `node research/random-discard/browser/fixtures.cjs $calculator $newInputsJson`.
