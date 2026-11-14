# Source owners

| Concern | Repository owner |
| --- | --- |
| Public execution and validation | `src/CardProbability/CalculationEngine.cs` |
| Immutable inputs and terminal results | `src/CardProbability/CalculationRequest.cs`, `CalculationContracts.cs` |
| Preparation and solver selection | `src/CardProbability/Internal/PreparedCalculation.cs`, `ProbabilityCalculation.cs` |
| Selectors, alternatives, events | `src/CardProbability/Internal/Compilation/` |
| Exact weights and unions | `src/CardProbability/Internal/Counting/` |
| Draw phases and physical state | `src/CardProbability/Internal/DrawResolution/` |
| Physical searches and survival | `src/CardProbability/Internal/Search/` |
| Proof and limits | `src/CardProbability/Internal/Certification/`, `Resources/` |
| Neutral regressions and independent oracles | `tests/CardProbability.EngineTests/`, its `Fixtures/` |
| Public package consumption and inspection | `tests/PackageConsumer/`, `tests/PackageInspection/` |
| Examples, packaging and release | `samples/`, `scripts/`, `.github/workflows/` |

Hosts own domain/catalog translation, policy keys, persistence, UI, asynchronous execution and worker transport/lifecycle. Keep those concerns outside this library. The engine's closed declarative model is not a general gameplay framework.
