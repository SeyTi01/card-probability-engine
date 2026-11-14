using CardProbability;

CalculationRequest request = new(
    entries: [new DeckEntry("lantern", 2), new DeckEntry("stone", 8)],
    handSize: 7,
    goals: [new Goal("light", [Requirement.OfEntry("lantern")])]
);
CalculationResult result = CalculationEngine.Calculate(request, new(CalculationMode.OpeningHand));
if (result.Status != CalculationStatus.Completed) {
    throw new InvalidOperationException(result.Detail);
}

Console.WriteLine($"Model {result.ModelVersion}: {result.Total!.Primary.Numerator}/{result.Total.Primary.Denominator}");
// C(10,7) = 120 equally likely hands; only C(8,7) = 8 contain no lantern.
RationalProbability expected = new("14", "15");
if (result.Total.Primary != expected || result.Total.Accuracy != ProbabilityAccuracy.Exact
    || result.Total.Certified != new CertifiedInterval(expected, expected)) {
    throw new InvalidOperationException("The independently counted opening-hand result must be exact 14/15.");
}
