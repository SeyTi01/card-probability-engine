using CardProbability.EngineTests.Fixtures;
using CardProbability.Internal;
using CardProbability.Internal.Counting;
using CardProbability.Internal.DrawResolution;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;
using Core = CardProbability.Internal.Contracts;

namespace CardProbability.EngineTests;

[TestFixture]
public sealed class RandomDiscardRevealTest {
    [TestCase("split")]
    [TestCase("ordinary-sibling")]
    [TestCase("fifo")]
    [TestCase("growth")]
    [TestCase("cost")]
    [TestCase("removal")]
    [TestCase("failed-removal")]
    [TestCase("hidden")]
    [TestCase("mill")]
    [TestCase("first")]
    [TestCase("lock")]
    [TestCase("minimum")]
    [TestCase("fetch-reveal")]
    [TestCase("ordered-search")]
    public void EveryRowMatchesPhysicalDiscardAndDeckEnumeration(string scenario) {
        foreach (bool ranked in new[] { false, true }) {
            EngineCase fixture = SmallCase(scenario, ranked);
            RevealSequenceOracle.Rational[] truth = RevealSequenceOracle.Count(fixture);
            foreach (CalculationMode mode in new[] { CalculationMode.Fast, CalculationMode.Exact }) {
                CalculationOptions options = new(mode, activationOrder: fixture.Options.ActivationOrder, priorityEntryIds: fixture.Options.PriorityEntryIds);
                CalculationResult result = CalculationEngine.Calculate(fixture.Request, options);
                Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
                ProbabilityValue[] rows = [result.Total!, .. result.Goals.Select(goal => goal.Value), .. result.Groups.Select(group => group.Value)];
                for (int row = 0; row < rows.Length; row++) {
                    RationalProbability expected = new(truth[row].Numerator.ToString(), truth[row].Denominator.ToString());
                    Assert.That(rows[row].Primary, Is.EqualTo(expected), $"{scenario}, {mode}, ranked={ranked}, row {row}");
                    Assert.That(rows[row].Certified, Is.EqualTo(new CertifiedInterval(expected, expected)));
                }
                Assert.That(CalculationEngine.CalculateUnion(fixture.Request, options).Total, Is.EqualTo(result.Total));
            }

            // Both production representations must retain the same physical FIFO and suffix mass.
            PreparedCalculation input = fixture.Prepare();
            DrawActivationPriority priority = ranked ? new(DrawActivationMode.CustomPriority, fixture.Options.PriorityEntryIds) : DrawActivationPriority.Default;
            foreach (PhysicalEnumerationMethod method in new[] { PhysicalEnumerationMethod.Frontier, PhysicalEnumerationMethod.Streaming }) {
                WorkBudget budget = new(Core.CalculationWorkPolicy.Unlimited);
                PostDrawCalculation calculation = new(input.Cards, fixture.Request.HandSize, budget, priority);
                ExactCalculationResult result = calculation.CalculateSearch(input.Combos, input.Groups, false, new(input.Cards, budget), physical: true, method: method);
                ExactProbability[] rows = result.ToRows();
                for (int row = 0; row < rows.Length; row++) {
                    Assert.That(rows[row].Numerator * truth[row].Denominator, Is.EqualTo(truth[row].Numerator * rows[row].Denominator), $"{method}, row {row}");
                }
            }
        }
    }

    [TestCase(0, "0", "1")]
    [TestCase(1, "0", "1")]
    [TestCase(3, "1", "1")]
    public void EmptyHandSelfDiscardAndEmptyDeck(int opening, string numerator, string denominator) {
        CalculationRequest request = new([Reveal("r", "key"), new("a", 1), new("b", 1)], opening, [new("source", [Requirement.OfEntry("r")])]);
        Assert.That(CalculationEngine.Calculate(request).Total!.Primary, Is.EqualTo(new RationalProbability(numerator, denominator)));
        Assert.That(CalculationEngine.Calculate(request, new(CalculationMode.OpeningHand)).Total!.Probability, Is.EqualTo(opening / 3d));
    }

    [TestCase(false, "2", "3", "0", "1")]
    [TestCase(true, "5", "8", "1", "12")]
    public void ExcavationGateObservesOnlyTheBranchThatDrew(bool gate, string n, string d, string xn, string xd) {
        CalculationRequest request = new(
            [Reveal("r", "key"), new("x", 1, new(Excavation: new(1, gate))), new("target", 1), new("blank", 1)],
            2,
            [new("target", [Requirement.OfEntry("target")]), new("excavator", [Requirement.OfEntry("x")])]
        );
        // Enumerate 24 labeled orders, splitting a held reveal equally between its two discards.
        // Opening with the reveal and drawing the excavator leaves it held on 1/12 of all outcomes.
        foreach (CalculationMode mode in new[] { CalculationMode.Fast, CalculationMode.Exact }) {
            CalculationResult result = CalculationEngine.Calculate(request, new(mode));
            Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
            Assert.That(result.Goals[0].Value.Primary, Is.EqualTo(new RationalProbability(n, d)));
            Assert.That(result.Goals[1].Value.Primary, Is.EqualTo(new RationalProbability(xn, xd)));
        }
    }

    [Test]
    public void DistinctUnmatchedKeysFitTheSamePhysicalPartition() {
        List<DeckEntry> ordinary = Enumerable.Range(0, 24).Select(index => new DeckEntry($"blank-{index}", 1)).ToList();
        Goal[] goals = [new("prize", [Requirement.OfEntry("prize")])];
        CalculationRequest Request(IEnumerable<DeckEntry> fillers) => new(
            [Reveal("r", "shared"), new("sibling", 1) { RevealIdentity = "shared" }, new("prize", 1), .. fillers], 3, goals);
        CalculationOptions limits = new(policy: new(WorkUnits: 400_000, CellLimit: 2048));
        CalculationResult plain = CalculationEngine.Calculate(Request(ordinary), limits);
        CalculationResult keyed = CalculationEngine.Calculate(Request(ordinary.Select(entry => entry with { RevealIdentity = entry.Id })), limits);
        Assert.That(plain.Status, Is.EqualTo(CalculationStatus.Completed));
        Assert.That(keyed.Status, Is.EqualTo(CalculationStatus.Completed), keyed.Detail);
        Assert.That(keyed.Total, Is.EqualTo(plain.Total));
    }

    [Test]
    public void RevealExhaustionAndCancellationReturnNoProbabilityRows() {
        CalculationRequest request = SmallCase("growth").Request;
        Assert.That(CalculationEngine.Assess(request).ElevatedRisk, Is.True);
        Check(CalculationEngine.Calculate(request, new(policy: new(WorkUnits: 1_000))), CalculationStatus.WorkLimit);
        Check(CalculationEngine.Calculate(request, new(policy: new(CellLimit: 64))), CalculationStatus.StorageLimit);
        Check(CalculationEngine.CalculateUnion(request, cancellationToken: new(true)), CalculationStatus.Cancelled);

        static void Check(CalculationResult result, CalculationStatus expected) {
            Assert.That(result.Status, Is.EqualTo(expected), result.Detail);
            Assert.That(result.Total, Is.Null);
            Assert.That(result.Goals, Is.Empty);
            Assert.That(result.Groups, Is.Empty);
        }
    }

    [Test]
    public void InvalidOrMixedRevealPlansFailEvenWhenOperationsAreSuppressed() {
        DeckEntry valid = Reveal("r", "key");
        EffectPlan reveal = valid.Effect!;
        foreach (DeckEntry entry in new[] { valid with { RevealIdentity = null }, valid with { RevealIdentity = " " } }) {
            Check(entry, CalculationStatus.InvalidInput);
        }
        foreach (EffectPlan mixed in new[] { reveal with { DrawCount = 1 }, reveal with { HandCost = 1 }, reveal with { RemovalCount = 1 },
            reveal with { MinimumHandSize = 1 }, reveal with { HiddenDeckCost = 1 }, reveal with { HiddenPostDrawMill = 1 },
            reveal with { FirstActivationOnly = true }, reveal with { BlocksLaterDraws = true }, reveal with { ActivationLimit = 1 },
            reveal with { Search = new(Eligibility.Any) }, reveal with { Excavation = new(1) } }) {
            Check(valid with { Effect = mixed }, CalculationStatus.Unsupported);
        }

        static void Check(DeckEntry entry, CalculationStatus status) {
            CalculationRequest request = new([entry], 1, [new("goal", [])]);
            foreach (CalculationMode mode in new[] { CalculationMode.OpeningHand, CalculationMode.Exact }) {
                CalculationResult result = CalculationEngine.Calculate(request, new(mode));
                Assert.That(result.Status, Is.EqualTo(status));
                Assert.That(result.Total, Is.Null);
            }
        }
    }

    internal static EngineCase SmallCase(string scenario, bool ranked = false) {
        DeckEntry source = Reveal("r", "shared", scenario is "fifo" or "ordered-search" ? 1 : 2);
        DeckEntry second = scenario switch {
            "split" => Reveal("s", "shared"),
            "ordinary-sibling" => new("s", 1) { RevealIdentity = "shared" },
            _ => Reveal("s", "Shared")
        };
        DeckEntry extra = scenario switch {
            "fifo" => new("once", 2, new(DrawCount: 1, RemovalCount: 1)),
            "growth" => new("growth", 1, new(DrawCount: 3, ActivationLimit: null)),
            "cost" => new("cost", 1, new(DrawCount: 2, HandCost: 1, HandCostEligibility: new("red"))),
            "removal" => new("remove", 1, new(DrawCount: 2, RemovalCount: 1, RemovalEligibility: new("blue"))),
            "failed-removal" => new("remove", 1, new(DrawCount: 1, RemovalCount: 1, RemovalEligibility: new("none"))),
            "hidden" => new("hidden", 1, new(DrawCount: 1, HiddenDeckCost: 1)),
            "mill" => new("mill", 1, new(DrawCount: 1, HiddenPostDrawMill: 1)),
            "first" => new("first", 1, new(DrawCount: 2, FirstActivationOnly: true)),
            "lock" => new("lock", 1, new(DrawCount: 1, BlocksLaterDraws: true)),
            "minimum" => new("minimum", 1, new(DrawCount: 2, MinimumHandSize: 3)),
            "fetch-reveal" => new("search", 1, new(Search: new(new("sources")))),
            "ordered-search" => new("search", 2, new(HandCost: 1, Search: new(new("red")))),
            _ => new("plain", 1)
        };
        DeckEntry last = scenario switch {
            "ordered-search" => new("other-search", 1, new(Search: new(new("blue")))),
            "cost" or "removal" or "hidden" or "mill" or "first" or "lock" or "minimum" => new("search", 1, new(Search: new(new("red")))),
            _ => new("blank", 1)
        };
        List<DeckEntry> deck = [source, second, extra, new("ab", 1), new("b", 1), last];
        Membership[] memberships = [new("red", ["ab", "s"]), new("blue", ["ab", "b"]), new("sources", ["r", "s"]), new("none", [])];
        Goal[] goals = [new("distinct", [Requirement.InMembership("red"), Requirement.InMembership("blue")], groupId: "g"),
            new("excluded", [Requirement.OfEntry("r", 0, 0), Requirement.OfEntry(extra.Id, 1)], groupId: "g"),
            new("sibling", [Requirement.OfEntry("s", 1)]), new("survived", [])];
        return new(scenario, new(deck, 3, goals, memberships, ["g", "empty"]),
            ranked ? new(activationOrder: ActivationOrder.Priority, priorityEntryIds: [extra.Id, "s", "r"]) : new());
    }

    private static DeckEntry Reveal(string id, string identity, int copies = 1) =>
        new(id, copies, new(ActivationLimit: null) { RandomDiscardReveal = true }) { RevealIdentity = identity };
}
