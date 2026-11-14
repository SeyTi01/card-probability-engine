using CardProbability;

(string Name, EffectPlan Plan, RationalProbability Expected)[] examples = [
    ("Draw", new(DrawCount: 1), new("1", "3")),
    ("Search", new(Search: new SearchOperation(new Eligibility("targets"))), new("1", "2")),
    ("Excavation", new(Excavation: new ExcavationOperation(2)), new("5", "12"))
];
foreach ((string name, EffectPlan plan, RationalProbability expected) in examples) {
    CalculationRequest request = new(
        entries: [new("source", 1, plan), new("target", 1), new("blank", 2)],
        handSize: 1,
        goals: [new Goal("reach", [Requirement.OfEntry("target")])],
        memberships: [new Membership("targets", ["target"])]
    );
    CalculationResult result = CalculationEngine.Calculate(request); // Exact by default.
    if (result.Status != CalculationStatus.Completed) {
        throw new InvalidOperationException(result.Detail);
    }

    Console.WriteLine($"{name}: {result.Total!.Primary.Numerator}/{result.Total.Primary.Denominator}");
    // Opening target succeeds in 1/4. Opening source is also 1/4, then succeeds
    // with probability 1/3 (draw), 1 (search), or 2/3 (reveal two and keep one).
    if (result.Total.Primary != expected || result.Total.Accuracy != ProbabilityAccuracy.Exact
        || result.Total.Certified != new CertifiedInterval(expected, expected)) {
        throw new InvalidOperationException($"{name} must match its independently counted exact result.");
    }
}
