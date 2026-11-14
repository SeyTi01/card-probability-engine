# Accuracy and resources

[Package overview](../README.md) · [API guide](generic-calculation-api.md) · [Calculation model](calculation-model.md)

## Modes and proof

| Mode | Contract |
| --- | --- |
| `Exact` (default) | Complete physical-exact counting within the supported fixed policy, or fail without probability rows. |
| `Fast` | Use faster routes; a completed result can be exact, bounded or uncertified. |
| `OpeningHand` | Validate supplied plans, suppress their execution and count only the dealt hand. |
| `OptimisticSearchProjection` | Explicit compatibility mode for the historical optimistic search-access projection. |

`ProbabilityValue.Primary` is an exact rational for the selected primary model. Its decimal-string numerator and denominator preserve arbitrary-size integers across host transports. `Probability` is a convenience `double` for that fraction. Neither value is necessarily the physical answer of an uncertified Fast result.

| `Accuracy` | Meaning |
| --- | --- |
| `Exact` | The physical-model probability is proven. Certified endpoints coincide. |
| `Bounded` | Rational lower and upper endpoints enclose the physical-model probability. |
| `Uncertified` | A completed primary exists, but no usable physical interval was established. |

Inspect each goal, group and total row separately. A Fast projection can lie outside its certified interval. Equal endpoints can prove a physical fraction different from that projection. A certificate is a mathematical bound, not a statistical confidence interval. Hosts choosing a midpoint display should preserve its interval context and never present a nonzero interval as zero uncertainty.

Fast can prove exactness. Search-only requests and supported excavation requests use physical counting, and other routes may establish structural exact proof. When draw histories leave search-target survival unproven, Fast can use an identity-blind projection and attempt a separate certificate.

`CalculationResult.Certification` describes that optional attempt. `Limited` or `Error` can accompany a completed primary; they do not turn it into a failed calculation. Exhausting resources before the primary completes does return a failure without rows. `CalculateUnion` omits optional certification while retaining structural exactness established by the primary route, so it may report weaker proof than `Calculate` for the same primary fraction.

## Work and Storage

Work is deterministic operation accounting, not elapsed time. With no explicit policy, `Fast` has a 150,000,000-unit allowance; other modes use unlimited Work. An explicit `new ExecutionPolicy()` also uses unlimited Work, including when supplied to Fast.

Default per-structure Storage limits are 32,768 entries and 262,144 32-bit cells. Both limits are configurable positive integers and include retained integer payload in relevant structures. They are not total process-memory limits: several structures, temporary state and object overhead can coexist. Increasing Work does not resolve a Storage limit, and increasing either resource does not guarantee practical completion.

The weighted search counter can retain more subproblems at larger limits: up to 2,048 at the default limits, 8,192 at 65,536 entries / 524,288 cells, and 16,384 at 131,072 entries / 1,048,576 cells. Retention remains bounded by both entries and integer payload. A full optional cache stops retaining results without changing their mathematical counts. These thresholds are implementation details, not a promise of speed or permanent cache sizes.

`CalculationMetrics` reports charged Work and observed peaks of individual checked structures. It does not measure elapsed time or total/live process memory. State growth depends on overlap, ordered draws, costs/removals, competing searches and hidden losses; deck size alone is not a performance predictor. Solver capability and resource limits are reported through terminal outcomes.

`CalculationEngine.Assess` offers conservative structural guidance. `ElevatedRisk == false` means no warning was produced, including for some invalid, unsupported or assessment-budget-limited inputs. It is neither validation nor a completion guarantee.

## Host responsibilities

Calls are synchronous, stateless and request-local. Immutable request snapshots may be reused concurrently. Cancellation is cooperative through `CancellationToken`; a caller choosing a background thread or worker owns its lifecycle and stale-result handling.

Every call returns one terminal result. A staged Fast-then-Exact experience is host logic: preserve the accepted preliminary result if the upgrade is interrupted, distinguish its accuracy, and accept an upgrade only for the same captured input. The library owns no browser worker, timer, lease or transport schema.

Performance evidence should compare identical fixtures, runtime settings and build modes, with first and warm calls reported separately. Native measurements do not establish browser AOT behavior. Release integration evidence belongs with its exact source revision and package artifact; see [release procedure](contributing.md#release-procedure).
