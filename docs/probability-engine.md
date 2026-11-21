# Probability engine

[Package overview](../README.md) · [Supported model](calculation-model.md) · [Accuracy and performance](accuracy-and-performance.md)

## Physical copies and weighted hands

For a uniformly shuffled deck of N physical cards, every unordered opening hand of h copies has probability `1 / C(N,h)`. Copies of the same entry are still distinct physical cards. Grouping behaviorally equivalent copies is an optimization: selecting k copies from a group of n carries weight `C(n,k)`. For disjoint classes of sizes nᵢ, a count vector kᵢ has weight `∏ C(nᵢ,kᵢ)`; summing over `Σ kᵢ = h` gives `C(N,h)`. Pooling requires equivalent membership truth and future policy observations, not merely identical names. When encounter order affects resolution, ordered histories must retain their own multiplicities.

The engine compiles memberships and goal alternatives before counting. A hand-wide maximum counts all copies matching its target. Positive requirements additionally need an assignment of distinct copies. This is a bipartite matching condition: copies connect to the requirements they can fill. Hall-style subset constraints ensure every collection of required slots has enough eligible copies. Checking each minimum independently would miss collisions between overlapping roles.

## Worked example: ten equally likely hands

Consider five distinct cards, draw two, with no effects:

| Copy | Properties |
| --- | --- |
| x | Starter, Fire |
| y | Starter |
| z | Fire |
| f1, f2 | Neither |

There are `C(5,2) = 10` hands. Route A is two separate requirements, `Starter + Fire`. Route B is one same-card requirement, `Starter AND Fire`.

| Result | Winning hands | Probability |
| --- | --- | --- |
| A: distinct Starter and Fire | xy, xz, yz | 3/10 |
| B: a card with both properties | xy, xz, xf1, xf2 | 4/10 |
| A and B | xy, xz | 2/10 |
| A or B | xy, xz, yz, xf1, xf2 | 5/10 = 1/2 |

In `xf1`, x cannot fill both separate slots in A, but it satisfies B. Adding the two route percentages would give 7/10 and double-count xy and xz. The union is `3/10 + 4/10 - 2/10 = 1/2`.

This table was independently checked by enumerating all ten physical pairs. Reproduce it without calculator internals:

```python
from itertools import combinations
properties = {"x": {"Starter", "Fire"}, "y": {"Starter"},
              "z": {"Fire"}, "f1": set(), "f2": set()}
hands = list(combinations(properties, 2))
a = {h for h in hands if any(i != j and "Starter" in properties[i]
     and "Fire" in properties[j] for i in h for j in h)}
b = {h for h in hands if any({"Starter", "Fire"} <= properties[i] for i in h)}
assert (len(hands), len(a), len(b), len(a & b), len(a | b)) == (10, 3, 4, 2, 5)
```

## Overlapping alternatives and unions

OR alternatives compile into events; impossible alternatives can be removed and equivalent/redundant alternatives reduced. Multiple goals may describe overlapping events. The engine combines compatible intersections and uses exact union counting rather than summing independent percentages. Shared decision structures merge equivalent predicates so the same physical outcome is not counted repeatedly. Groups use the same union semantics over their own active members.

Hall constraints, inclusion-exclusion/event-union counting and decision diagrams serve different layers: valid distinct-copy assignment, union across successful events, and reuse of equivalent decisions. Their presence does not imply that every input uses one universal algorithm or that arbitrary cases avoid exponential growth.

## Weighted draw resolution

Without hand operations, a reduced count-state dynamic program can aggregate exchangeable draw histories. A state carries relevant remaining multiplicities, held effect copies, used once-per-turn entries and exact weight. Ordinary cards can be pooled when their identities cannot alter subsequent decisions.

Ordered resolution and hand operations need more state: hand composition, physical encounter-ordered candidates, activation priority, eligible costs/removals and remaining deck counts. Equivalent states can still merge. Labeled draw prefixes and the number of unseen suffixes supply exact integer weights, so a path is weighted by its random realizations rather than counted once merely because it is a distinct state. Requirements are evaluated after resolution; failed branches stay in the original denominator.

A compiled scenario conditions final-hand counting on what has been held, consumed or lost. These states are not additional independent decks. Weights already encode their conditional probability and are aggregated exactly. This avoids rounding every branch before summing.

### Random-discard weights

For a deck of `N` physical copies, let `H` be the opening size plus the sum of all possible positive net hand gains from fixed-size draws. Let `D` count physical copies whose identity matches an active reveal identity, including ordinary siblings. Each reveal reduces the settled hand by one and removes at least one of those `D` copies. Multiplying `N!` by `lcm(1..min(N,H))^min(D,H)` supplies enough integer units for every sequence of random hand choices. A discard of multiplicity `m` from a hand of size `h` takes `m/h` of its parent's weight. Discarding different occurrences of the same queued source can change the next activation, so only equivalent FIFO outcomes can share multiplicity.

For larger reveal schedules the solver groups complete draw batches by their counts and distinct effect orders. A full storage bucket can be drained into lower remaining-deck sizes before later contributions arrive. All continuations consume at least one visible deck copy before they return to a bucket. Accumulating exact prefix weights preserves terminal mass across drains. Failed branches remain in the common denominator.

### Delaying card identities

On eligible reveal mixtures, the solver follows the ordered effect cards and represents each other observed card by the labels it could still have. A random discard selects a physical held slot. If a mandatory removal needs to distinguish its label, the solver partitions assignments by the first eligible slot in a fixed order. The preceding slots exclude that label. The remaining slots keep their old possibilities. Every physical assignment belongs to exactly one branch, including branches where no eligible card exists.

At a terminal hand, binomial coefficients and multiplicities assign the remaining labels without replacement. Searches observe the physical target copies still available after losses. With no remaining identity-dependent decisions, all assignments of a given unobserved set of surviving slots have equal weight. Goal, group and total counts use the same hand and activation history.

Fast can also group reveal names with equal copy counts and activation ranks when every removal treats those names as one contiguous block in its ordered list. The state still records which held copies share a name and where they occur in the queue. Before the first comparison between those names, each assignment of names to the state has equal weight. The first comparison splits by the chosen name. If another comparison would need the earlier ordering information, the remaining branch contributes to the uncertainty interval. Exact keeps the original names throughout.

In these mixtures each ordinary effect preserves the settled hand size and each reveal reduces it by one. For an opening of size `h`, `h!` provides integer units for all random hand choices. A common unseen-deck suffix factor cancels from the numerator and denominator. Exact can drain a full state table into lower layers and continue counting. Fast can stop expanding a prefix. If the total mass is `D`, the classified mass is `C`, and one row has `W` classified wins, its physical probability lies in `[W/D, (W + D − C)/D]`. Prefix ranking affects how much mass gets classified. Both endpoints use integer counts.

## Physical searches and hidden losses

A single excavation resolves between draws and ordinary searches. For each physical hand/deck state, the fixed policy selects the eligible source with the largest excavation count. Each possible target addition is checked with the existing search-assignment rules, using only search opportunities held before that addition. For each goal or requested union, let `G` be the number of remaining physical copies whose addition succeeds, `D` the remaining deck size and `k` the excavation count. The conditional probability is `1 - C(D-G,k) / C(D,k)`: the revealed pool must contain at least one successful target. This counts a reveal once even if several choices work and avoids enumerating all `k`-card pools. Group and total rows use their own unions of successful targets, not sums of individual goal probabilities.

Search-only outcomes can be counted from held sources, eligible remaining copies and shared deck capacity. Search assignments compile into predicates on the final hand; decision diagrams merge equivalent successful choices and count their union with exact physical-copy weights.

Draw/search mixtures must also distinguish held cards from consumed/discarded copies and hidden deck losses. The efficient physical count-state path handles compatible draw batches. Ordered draws, costs and mandatory removals have a physical fallback retaining the identities required to count survival jointly. Selective survivor representations pool unobserved labels only when their conditional uniform-subset distribution and future decisions permit it. A target used up earlier cannot reappear as a remaining search target; competing searchers cannot claim the same surviving copy twice.

Fast can use a cheaper identity-blind search-access model when survival is not proven. Certification is a separate analysis around that model; its primary rational is not automatically the physical answer. See [accuracy](accuracy-and-performance.md#modes-and-proof).

## Rational results and source map

Integer counts and rational weights are combined before final display conversion. Returned primary rational rows preserve the modeled fraction. Certified lower/upper fractions are a separate contract. Rounded percentages are a presentation layer; a displayed midpoint need not equal the primary fraction.

The public [engine entry point](../src/CardProbability/CalculationEngine.cs) owns calculation execution and terminal outcomes. Its internal [counting](../src/CardProbability/Internal/Counting/), [draw resolution](../src/CardProbability/Internal/DrawResolution/), [search](../src/CardProbability/Internal/Search/), [certification](../src/CardProbability/Internal/Certification/), and [resource accounting](../src/CardProbability/Internal/Resources/) modules implement the model. Host adapters resolve their own predicates and effect definitions into neutral inputs and translate terminal outcomes into their own contracts.

The [public package consumer](../tests/PackageConsumer/Program.cs) checks fictional examples using independent physical-hand enumeration and basic combinatorics. The complete [neutral engine suite](../tests/CardProbability.EngineTests/) retains boundary, counting, effect, certification and optimization regressions; its [fixtures](../tests/CardProbability.EngineTests/Fixtures/) include independent sequence and search-certificate oracles.
