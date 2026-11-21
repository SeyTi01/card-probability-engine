# Exact Thunder Dragon player references

This standalone .NET 10 program calculates every row for the historical 40-card Thunder Dragon player list with five or six opening cards. It reads the same exported calculator request used by the browser workload and validates its card counts, memberships, effects, default encounter order and two goals. Its only dependency is the .NET base class library. The checked fractions are the two `thunder-dragon` entries in [the reference dataset](../../legacy-exact.json).

From the engine repository, export the fixtures using the calculator checkout described in [the browser replay guide](../README.md). Use a new output filename since the exporter preserves existing captures.

```powershell
$inputs = Join-Path $env:TEMP 'thunder-reference-inputs.json'
node research/random-discard/browser/fixtures.cjs $calculator $inputs
$oracle = 'research/random-discard/browser/thunder-reference/ThunderReference.csproj'
dotnet run --project $oracle -c Release -- 5 $inputs
dotnet run --project $oracle -c Release --no-build -- 6 $inputs
```

Each JSON output contains the fixture name, its canonical SHA-256, and exact rational rows in calculator order: total, Solar, Fusion, group. The program requires the summed physical probability of all successful and failed branches to equal exactly one before emitting a result.

## Physical histories

The exported player model has five distinct Danger identities with two physical copies each, three copies of Allure of Darkness, and no search operation. Allure draws two and must discard one DARK card. Among held DARK cards the fixed policy removes an unprotected ordinary card first (two copies of Chaos Dragon Levianeer), then a usable Danger, then a goal-relevant Thunder Dragon monster (five DARK copies). The complete draw history visits at most 18 deck cards, so the remaining deck can always supply these operations.

The queue retains a token for each encountered active copy. Each Danger victim is a physical hand location. Removing a same-identity victim preserves the remaining queue; removing a different identity consumes the selected source and draws one. Allure retains the canonical order when several different Danger identities are eligible. Removing a particular identity discards its latest token.

All five Danger identities have the same multiplicity, are equally eligible as DARK material, and have the same activation priority. Before their canonical removal ranks are observed, absent identities with equal remaining stock can be exchanged. The oracle weights a representative by the number of such identities. When a removal must choose among distinct identities, it tracks the relative-rank comparisons implied by each possible winner. Every later comparison respects earlier ones. At terminal, the compatible permutations among the five physical names give the number of assignments for that history. The starting denominator includes all 120 permutations.

An ordered prefix after `d` visible cards and `r` reveals uses denominators `(40)_d` and `(H)_r`, where `(n)_k` denotes a falling factorial and `H` is the opening hand size. Allure preserves settled hand size and each Danger reveal reduces it by one. The program aggregates physical multiplicities in checked 128-bit integers, then combines strata in arbitrary-precision arithmetic. A prefix that cannot retain two distinct cards is a proven failure for both goals.

## Conditional goal counts

The remaining 20 cards are ordinary, non-DARK cards. They include three Batteryman Solar, three Thunder Dragon Fusion, two light Thunder Dragon monsters, and twelve other cards. Allure cannot select these cards as DARK material. Random discards observe only their aggregate count. Conditional on retaining `k` cards from this pool, their physical identities form a uniform subset of size `k`.

For a surviving DARK Thunder Dragon monster, the number of winning pool subsets is `C(20,k) − C(20−x,k)`, with `x=3` for either individual goal and `x=6` for their union. Without a surviving DARK Thunder monster, a winning subset must also contain one of the two light Thunder monsters. Subtract `C(18,k) − C(18−x,k)` in that case. The two three-copy goals consequently have equal physical probabilities, and the only reporting group equals the total union.

The oracle uses this conditional combinatorial calculation directly. It does not load the engine, its compiled goal evaluator, its calculation results, or its search and terminal caches.
