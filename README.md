# CardProbability.Engine

Calculate probabilities for a finite deck of physical copies, overlapping memberships and goals, with optional draw, search and excavation operations. Results preserve exact rational values and distinguish a proven physical probability from a faster projection or certified interval.

The package targets **.NET 10** and uses only the .NET base class library. Its public namespace and assembly name are `CardProbability`. The stable version is **`1.0.0`**.

## Install

In a .NET 10 project:

```sh
dotnet add package CardProbability.Engine --version 1.0.0
```

## Calculate an opening hand

A ten-copy deck contains two lanterns and eight stones. A seven-copy hand contains a lantern in `112 / 120 = 14 / 15` equally likely hands.

```csharp
using CardProbability;

CalculationRequest request = new(
    entries: [
        new DeckEntry("lantern", 2),
        new DeckEntry("stone", 8)
    ],
    handSize: 7,
    goals: [
        new Goal("light", [Requirement.OfEntry("lantern")])
    ]
);

CalculationResult result = CalculationEngine.Calculate(request, new(CalculationMode.OpeningHand));

if (result.Status != CalculationStatus.Completed) {
    throw new InvalidOperationException(result.Detail);
}

Console.WriteLine($"Model {result.ModelVersion}: {result.Total!.Primary.Numerator}/{result.Total.Primary.Denominator}");
// Model 1: 14/15
```

The [basic sample](https://github.com/SeyTi01/card-probability-engine/blob/main/samples/BasicCalculation/Program.cs) runs this example. The [effects sample](https://github.com/SeyTi01/card-probability-engine/blob/main/samples/SupportedEffects/Program.cs) demonstrates draw, search and excavation requests with independently known results. The [API guide](https://github.com/SeyTi01/card-probability-engine/blob/main/docs/generic-calculation-api.md) also covers overlapping memberships, distinct physical copies, `(A OR B) AND C`, resource limits and terminal outcomes.

## Model and results

- Positive requirements for different targets need distinct physical copies. Maxima count every matching copy. Goal, group and total unions count an overlapping outcome once.
- Supported effects follow a fixed policy: automatic draws, at most one excavation, then optional searches. Newly drawn effects may activate; searched or excavated effects do not.
- `Exact` is the default mode and returns physical-exact results within this model, or a failure without probability rows. `Fast` can return exact, bounded or uncertified results. `OpeningHand` validates supplied plans while suppressing their execution.
- A primary fraction is exact for the selected counting model. Inspect each row's `Accuracy` and `Certified` interval before treating a Fast primary as a physical probability.
- The API is synchronous and supports cooperative cancellation. Hosts own asynchronous execution, staged results, stale-input handling, transport and persistence.

This is a closed mathematical model, not a card-text interpreter or general gameplay simulator. It does not model arbitrary callbacks, recursive fetched effects, multiple excavations or optimal effect sequencing. See the [model](https://github.com/SeyTi01/card-probability-engine/blob/main/docs/calculation-model.md) and [accuracy/resource contracts](https://github.com/SeyTi01/card-probability-engine/blob/main/docs/accuracy-and-performance.md).

## Develop

Install the .NET 10 SDK selected by [global.json](https://github.com/SeyTi01/card-probability-engine/blob/main/global.json), plus Node.js for validation scripts. No browser or WebAssembly workload is needed to build the library.

```sh
dotnet run --project samples/BasicCalculation
dotnet run --project samples/SupportedEffects
node scripts/validate.mjs
```

Validation runs the neutral tests and samples, packs Release artifacts, checks package metadata and Source Link, and runs public consumers restored from the actual package into a fresh isolated cache. See [contributing and releases](https://github.com/SeyTi01/card-probability-engine/blob/main/docs/contributing.md).

The [YGO calculator](https://github.com/SeyTi01/ygo-advanced-probability-calculator-web) is the engine's original project and serves as its pilot application. [Provenance](https://github.com/SeyTi01/card-probability-engine/blob/main/docs/provenance.md) records the original revision and source inventory.

Package versions and runtime `ModelVersion` are separate contracts.

## License

[MIT](https://github.com/SeyTi01/card-probability-engine/blob/main/LICENSE), with the original copyright attribution preserved.
