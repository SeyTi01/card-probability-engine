using System.Numerics;
using CardProbability.EngineTests.Fixtures;
using CardProbability.Internal;
using CardProbability.Internal.Certification;
using CardProbability.Internal.DrawResolution;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;
using Core = CardProbability.Internal.Contracts;

namespace CardProbability.EngineTests;

[TestFixture]
public sealed class SymbolicRevealTest {
    private static EngineCase MaskedCase(bool ranked = false) {
        DeckEntry[] entries = [
            new("r", 1, new(ActivationLimit: null) { RandomDiscardReveal = true }) { RevealIdentity = "r" },
            new("filter", 1, new(DrawCount: 2, ActivationLimit: null, RemovalCount: 1, RemovalEligibility: new("left"))),
            new("search", 1, new(Search: new(new("bridge-only")))),
            new("bridge", 1), new("material", 1), new("other", 1), new("blank", 1)
        ];
        Membership[] memberships = [
            new("left", ["bridge", "material"]), new("right", ["bridge", "other"]), new("bridge-only", ["bridge"])
        ];
        Goal[] goals = [
            new("pair", [Requirement.InMembership("left"), Requirement.InMembership("right")], groupId: "g"),
            new("bridge", [Requirement.OfEntry("bridge")], groupId: "g"),
            new("other-without-bridge", [Requirement.OfEntry("other"), Requirement.OfEntry("bridge", 0, 0)], groupId: "h")
        ];
        return new("masked", new(entries, 3, goals, memberships, ["g", "h"]), ranked
            ? new(activationOrder: ActivationOrder.Priority, priorityEntryIds: ["filter"]) : new());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FilteredFailuresAndDepletedSearchTargetsMatchPhysicalPermutations(bool ranked) {
        EngineCase fixture = MaskedCase(ranked);
        RevealSequenceOracle.Rational[] truth = RevealSequenceOracle.Count(fixture);
        WorkBudget budget = new(Core.CalculationWorkPolicy.Unlimited);
        PreparedCalculation input = fixture.Prepare(budget);
        DrawActivationPriority priority = Optimizations.DeferredTestCases.Priority(fixture);
        PostDrawCalculation calculation = new(input.Cards, fixture.Request.HandSize, budget, priority);
        // The only searchable target may already have been discarded. A mandatory
        // filter can also fail outright after its two visible draws.
        PostDrawCalculation.SymbolicRevealResult? result = calculation.TryCountSymbolicReveal(input.Combos, input.Groups);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Resolved, Is.EqualTo(result.Denominator));
        Assert.That(result.Wins, Has.Length.EqualTo(truth.Length));
        for (int row = 0; row < truth.Length; row++) {
            Assert.That(result.Wins[row] * truth[row].Denominator,
                Is.EqualTo(truth[row].Numerator * result.Denominator), $"ranked={ranked}, row {row}");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExchangeableRevealNamesPreserveObservedRemovalOrder(bool repeated) {
        DeckEntry[] reveals = Enumerable.Range(1, 3).Select(i => new DeckEntry($"r{i}", 1,
            new(ActivationLimit: null) { RandomDiscardReveal = true }) { RevealIdentity = $"r{i}" }).ToArray();
        DeckEntry[] entries = [.. reveals,
            new("filter", repeated ? 2 : 1, new(DrawCount: 2, ActivationLimit: null, RemovalCount: 1, RemovalEligibility: new("reveal"))),
            new("a", 1), new("b", 1), new("blank", repeated ? 5 : 2)
        ];
        CalculationRequest request = new(entries, repeated ? 3 : 2,
            [new("a", [Requirement.OfEntry("a")], groupId: "g"),
                new("b-only", [Requirement.OfEntry("b"), Requirement.OfEntry("a", 0, 0)], groupId: "g")],
            [new("reveal", reveals.Select(entry => entry.Id))], ["g"]);
        EngineCase fixture = new("reveal names", request, new(activationOrder: ActivationOrder.Priority, priorityEntryIds: ["filter"]));
        DrawActivationPriority priority = Optimizations.DeferredTestCases.Priority(fixture);
        PreparedCalculation input = fixture.Prepare();
        RevealSequenceOracle.Rational[] expected;
        if (repeated) {
            // Both copies can compare the remaining names. Use the complete
            // physical frontier for this twelve-card deck.
            WorkBudget budget = new(Core.CalculationWorkPolicy.Unlimited);
            PostDrawCalculation legacy = new(input.Cards, request.HandSize, budget, priority);
            expected = legacy.CalculateSearch(input.Combos, input.Groups, false, new(input.Cards, budget), physical: true,
                method: PhysicalEnumerationMethod.Frontier).ToRows()
                .Select(row => new RevealSequenceOracle.Rational(row.Numerator, row.Denominator)).ToArray();
        }
        else {
            expected = RevealSequenceOracle.Count(fixture);
        }
        foreach (bool exhaustive in new[] { false, true }) {
            WorkBudget budget = new(Core.CalculationWorkPolicy.Unlimited);
            PostDrawCalculation calculation = new(input.Cards, request.HandSize, budget, priority);
            PostDrawCalculation.SymbolicRevealResult? result = calculation.TryCountSymbolicReveal(input.Combos, input.Groups, exhaustive);
            Assert.That(result, Is.Not.Null);
            BigInteger unresolved = result!.Denominator - result.Resolved;
            if (exhaustive) {
                Assert.That(unresolved, Is.EqualTo(BigInteger.Zero));
            }
            Assert.That(result.Wins, Has.Length.EqualTo(expected.Length));
            for (int row = 0; row < expected.Length; row++) {
                BigInteger truth = expected[row].Numerator * result.Denominator;
                Assert.That(result.Wins[row] * expected[row].Denominator <= truth
                    && truth <= (result.Wins[row] + unresolved) * expected[row].Denominator, Is.True,
                    $"row={row}, exhaustive={exhaustive}, repeated={repeated}");
            }
        }
        CalculationOptions options = new(CalculationMode.Exact,
            activationOrder: fixture.Options.ActivationOrder, priorityEntryIds: fixture.Options.PriorityEntryIds);
        CalculationResult resultExact = CalculationEngine.Calculate(request, options);
        CalculationResult union = CalculationEngine.CalculateUnion(request, options);
        Assert.That(resultExact.Status, Is.EqualTo(CalculationStatus.Completed), resultExact.Detail);
        Assert.That(resultExact.Mode, Is.EqualTo(CalculationMode.Exact));
        Assert.That(resultExact.Accuracy, Is.EqualTo(ProbabilityAccuracy.Exact));
        Assert.That(union.Total, Is.EqualTo(resultExact.Total));
        ProbabilityValue[] exactRows = [resultExact.Total!, .. resultExact.Goals.Select(goal => goal.Value), .. resultExact.Groups.Select(group => group.Value)];
        for (int row = 0; row < expected.Length; row++) {
            RationalProbability primary = exactRows[row].Primary;
            Assert.That(exactRows[row].Certified, Is.EqualTo(new CertifiedInterval(primary, primary)));
            Assert.That(BigInteger.Parse(primary.Numerator) * expected[row].Denominator,
                Is.EqualTo(BigInteger.Parse(primary.Denominator) * expected[row].Numerator));
        }
    }

    [TestCase(8, false)]
    [TestCase(10, false)]
    [TestCase(10, true)]
    public void CertificateUsesEachUnionWinningMassAndReportsItsUpperEndpoint(int denominator, bool unionOnly) {
        PreparedCalculation input = MaskedCase().Prepare();
        WorkBudget budget = new(Core.CalculationWorkPolicy.Unlimited);
        // Among eight resolved histories five win A, three win B and two win both.
        // Their union has six wins. C adds one disjoint winner to the total.
        int[] wins = [7, 5, 3, 1, 6, 1];
        Core.ProbabilityCalculationResult result = RevealCertificate.Complete(
            wins.Select(value => (BigInteger)value).ToArray(), 8, denominator, input.Combos, input.Groups, unionOnly, budget
        );
        Core.SearchAvailabilityCertificate certificate = result.SearchAvailability!;
        Core.CalculationFractions primary = result.Contract!.Fractions!;
        Core.SearchAvailabilityInterval[] rows = [certificate.Total!, .. certificate.Combos, .. certificate.Groups];
        Core.ProbabilityFraction[] fractions = [primary.Total, .. primary.Combos, .. primary.Groups];
        bool exact = denominator == 8;
        Assert.That(certificate.Status, Is.EqualTo(exact ? Core.SearchAvailabilityStatus.Exact : Core.SearchAvailabilityStatus.Bounded));
        Assert.That(rows, Has.Length.EqualTo(unionOnly ? 1 : wins.Length));
        for (int row = 0; row < rows.Length; row++) {
            Check(rows[row].Lower, wins[row]);
            Check(rows[row].Upper, wins[row] + denominator - 8);
            Assert.That(fractions[row], Is.EqualTo(rows[row].Upper));
            Assert.That(rows[row].IsExact, Is.EqualTo(exact));
        }

        void Check(Core.ProbabilityFraction fraction, int n) =>
            Assert.That(BigInteger.Parse(fraction.Numerator) * denominator, Is.EqualTo(n * BigInteger.Parse(fraction.Denominator)));
    }

    [Test]
    public void PublicFastCertifiesTheFirstNonDrawCopyAfterAChainOfDraws() {
        // Five hand-neutral draws skip themselves. The first of A, B and the
        // reveal is uniform. With a one-card hand the reveal always discards itself.
        // Thus A and B each win 1/3, and their union wins 2/3.
        CalculationRequest request = new([
            new("draw", 5, new(DrawCount: 1, ActivationLimit: null)),
            new("reveal", 1, new(ActivationLimit: null) { RandomDiscardReveal = true }) { RevealIdentity = "reveal" },
            new("a", 1), new("b", 1)
        ], 1, [new("a", [Requirement.OfEntry("a")], groupId: "g"), new("b", [Requirement.OfEntry("b")], groupId: "g")],
            groups: ["g"]);
        CalculationOptions options = new(CalculationMode.Fast);
        CalculationResult result = CalculationEngine.Calculate(request, options);
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
        ProbabilityValue[] rows = [result.Total!, .. result.Goals.Select(goal => goal.Value), .. result.Groups.Select(group => group.Value)];
        int[] numerators = [2, 1, 1, 2];
        for (int i = 0; i < rows.Length; i++) {
            CertifiedInterval? certificate = rows[i].Certified;
            Assert.That(certificate, Is.Not.Null);
            Assert.That(rows[i].Primary, Is.EqualTo(certificate!.Upper));
            Assert.That(BigInteger.Parse(certificate.Lower.Numerator) * 3 <= numerators[i] * BigInteger.Parse(certificate.Lower.Denominator)
                && BigInteger.Parse(certificate.Upper.Numerator) * 3 >= numerators[i] * BigInteger.Parse(certificate.Upper.Denominator), Is.True);
        }
        CalculationResult union = CalculationEngine.CalculateUnion(request, options);
        Assert.That(union.Total, Is.EqualTo(result.Total));
        Assert.That(union.Goals, Is.Empty);
        Assert.That(union.Groups, Is.Empty);
    }

    [TestCase(CalculationMode.Fast, CalculationStatus.WorkLimit)]
    [TestCase(CalculationMode.Fast, CalculationStatus.StorageLimit)]
    [TestCase(CalculationMode.Fast, CalculationStatus.Cancelled)]
    [TestCase(CalculationMode.Exact, CalculationStatus.WorkLimit)]
    [TestCase(CalculationMode.Exact, CalculationStatus.StorageLimit)]
    [TestCase(CalculationMode.Exact, CalculationStatus.Cancelled)]
    public void InterruptedCalculationProvidesNoProbabilityRows(CalculationMode mode, CalculationStatus expected) {
        CalculationRequest request = MaskedCase().Request;
        ExecutionPolicy policy = expected == CalculationStatus.StorageLimit ? new(CellLimit: 64) : new(WorkUnits: 1_000);
        CalculationResult result = CalculationEngine.Calculate(request, new(mode, policy), new CancellationToken(expected == CalculationStatus.Cancelled));
        Assert.That(result.Status, Is.EqualTo(expected), result.Detail);
        Assert.That(result.Total, Is.Null);
        Assert.That(result.Goals, Is.Empty);
        Assert.That(result.Groups, Is.Empty);
    }
}
