# Calculation model

[Package overview](../README.md) · [API guide](generic-calculation-api.md) · [Accuracy and resources](accuracy-and-performance.md)

## Inputs and success

A request describes a uniformly shuffled finite deck of physical copies, an opening hand size, resolved memberships, goals and optional operations. IDs are opaque, ordinal and case-sensitive. The engine does not infer properties or operations from an entry's name.

Each physical copy fills at most one positive requirement for a chosen goal route. Different goals are evaluated independently. Requirements have inclusive minimum and maximum counts. A missing maximum means the evaluated hand size; `0/0` excludes every matching copy. A maximum counts all matching copies, including copies assigned to a different positive requirement. Repeated references to the same entry or membership intersect their bounds; use a larger minimum to request several copies.

Choices within one `Alternative` are OR. Separate alternative groups and the goal's required list are AND. A same-copy intersection is represented by a membership containing exactly the entries in that intersection. It differs from two positive requirements, which need distinct copies.

Each goal reports its success probability. A reporting group is the union of its goals; the total is the union of all goals. An outcome satisfying several routes, goals or legal search assignments contributes once to each relevant union.

## Automatic draws

Fixed-size draw operations activate automatically and consume their source. An entry's activation allowance is shared by its physical copies; separate entries have independent allowances. `ActivationLimit: null` removes the entry cap, so another held or newly drawn source copy may activate. All other legality checks still apply.

Encounter order is the default. With `ActivationOrder.Priority`, listed source entries rank highest in the supplied order, with physical encounter order breaking ties. Priorities apply separately within draws and searches; they do not reorder phases. A selected activation finishes its full draw batch, hidden milling and mandatory removal before another activation is selected. Newly drawn higher-priority sources can overtake waiting ones.

Hand costs remove eligible other held copies before drawing. The source cannot pay its own cost. The complete hidden-deck cost, draw batch and hidden post-draw mill must fit in the remaining deck. Minimum-hand and first-activation restrictions must also hold. Otherwise the source stays in hand without spending its allowance or costs. A source lacking hand-cost material is deferred and retried after another activation; a pass with no activation ends draw resolution. A batch blocked by deck capacity cannot become legal later because capacity only decreases.

Mandatory removals happen after drawing and can remove newly drawn copies. The source cannot be removed again. Insufficient eligible removal material fails that branch; its probability remains in the original denominator. `FirstActivationOnly` permits a draw only before another draw has activated. `BlocksLaterDraws` closes the draw phase after that activation.

## Random-discard reveals

A reveal enters the same activation queue as a draw. It can activate when draws are unlocked and at least one physical deck copy remains. Choose one uniformly random physical copy from the entire held hand, including the revealed source.

- If the selected copy has the source's `RevealIdentity`, discard only that copy. Any surviving source stays in its original queue position and can reveal again.
- Otherwise discard the selected copy, consume the revealed source, and draw one. A drawn source can activate in encounter or priority order.

Every reveal counts as an activation for `FirstActivationOnly`. Only a branch that actually draws disables a later excavation with `RequiresNoPriorEffectDraw`. The discard has no triggered effects. An ordinary or differently configured entry can share the reveal identity without acquiring the operation.

## Deterministic removal policy

The keep policy protects positive goal pieces and searches with plausible access to them, then other usable searches and draws, potentially usable cost-deferred draws, and other copies. Maximum-only constraints do not protect a copy. Search protection is local: it does not reserve competing targets or promise survival through later operations.

The policy depends on all active goal routes and is shared by individual, group and total rows. Changing the active goal set can therefore change a removal choice. It is not optimized separately for each result row.

`DeckEntry.PolicyOrderKey` is an advanced control over deterministic tie order. Supply keys for every entry or none. Keys may be empty or equal and compare ordinally. They sort the canonical policy partition before ordinary-entry-before-effect-source ordering and ordinal entry-ID tie-breaks. Without supplied keys, the engine derives a generic order from ordinal membership and direct-entry requirement bit strings. Hosts adopting this package must preserve any existing domain-derived keys when equivalent behavior matters. Activation priority and visual sorting are separate concepts.

## One excavation after draws

After draw resolution, at most one eligible held excavation source activates before searches. The highest reveal count wins; ordinal entry ID breaks ties. Selection happens before the reveal. The complete reveal must fit in the remaining deck. `RequiresNoPriorEffectDraw` excludes a source if an effect already drew cards on that branch; legal draws are not skipped to reserve an excavation.

The chosen source is consumed even if the reveal cannot help a goal. Exactly one revealed physical copy replaces it. The engine checks possible additions jointly with legal ordinary searches, preserving distinct-copy assignment, maxima, copy competition and overlap-safe unions. Unselected revealed copies remain available to searches. There are no subsequent draws or excavations, so no continuation observes their order.

An excavated effect does not activate. Only search opportunities held before the excavation addition may act afterward. Multiple sequential excavations are unsupported: delaying an earlier selection until a later reveal would change the information available to that choice.

## Optional searches

Searches happen after the draw and excavation phases. Eligible search sources may be declined or consumed to transfer one eligible remaining deck copy to hand. Sources drawn during the draw phase can search; sources acquired by excavation or search cannot create new search opportunities.

The highest-priority currently legal source acts or is declined at each step. Costs, target availability, minimum hand size, and entry or mode allowances are checked again after each activation. Declining retires that physical opportunity. A temporarily unavailable source can become legal after another search. Search operations may have their own hand costs and mandatory removals; failed mandatory removals remain failed branches.

Targets already held, consumed or lost are not available deck copies. Sources compete for physical targets and remaining deck capacity. With alternative search modes, the resolved union of mode target memberships must equal the declared overall targets, including zero-copy entries. Each activation chooses one legal mode and target. A mode allowance is shared across source copies in addition to the entry-wide allowance.

For a goal or union, the model asks whether some legal combination of optional searches under this source order succeeds. Different goals may have different successful choices. This does not turn automatic draws, excavation selection or deterministic removal into optimal play.

## Scope and boundaries

An entry is ordinary when `Effect` is null. A plan contains exactly one supported draw, random-discard reveal, search or excavation operation. Empty plans, mixed operations on one source, and unsupported field combinations return `Unsupported`. Invalid IDs, references and negative values return `InvalidInput`. See the [operation field table](generic-calculation-api.md#supported-operations-and-boundaries).

`OpeningHand` validates supplied plans but suppresses execution and counts only the dealt hand. Other modes apply the same fixed effect policy; `Exact` describes exactness within that policy. Fast's primary may use an optimistic search-survival projection where physical availability is not established; [accuracy and certification](accuracy-and-performance.md) describe what can be concluded from a result.

The engine does not simulate arbitrary game rules, opposing players, text interpretation, recursive acquired effects, multiple excavations, optional draw or excavation activation, or optimal sequencing/removal. More Work or Storage cannot make an unsupported model supported. A hypothetical input does not automatically bound the probability of a real game.

The implementation is rooted at [CalculationEngine](../src/CardProbability/CalculationEngine.cs). [Draw resolution](../src/CardProbability/Internal/DrawResolution/), [search counting](../src/CardProbability/Internal/Search/) and the [neutral tests](../tests/CardProbability.EngineTests/) are maintained together in this repository.
