using System.Numerics;
using System.Text.Json;
using CardProbability.EngineTests.Fixtures;
using CardProbability.Internal;
using CardProbability.Internal.DrawResolution;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;
using Core = CardProbability.Internal.Contracts;

namespace CardProbability.EngineTests;

[TestFixture, Explicit("Opt-in random-discard bounds research"), NonParallelizable]
public sealed class RevealBoundsExperiment {
    private sealed record Bounds(BigInteger[] Lower, BigInteger[] Upper, BigInteger Denominator, long FinalizationWork, string Stop);

    [TestCase("beam")]
    [TestCase("continuation")]
    [TestCase("anonymous")]
    [TestCase("batch")]
    [TestCase("strata")]
    public void EnclosesLabeledOutcomesAtSeveralBudgets(string method) {
        foreach (string scenario in new[] { "ordinary-sibling", "growth", "lock", "failed-removal", "fetch-reveal" }) {
            EngineCase fixture = RandomDiscardRevealTest.SmallCase(scenario);
            RevealSequenceOracle.Rational[] truth = RevealSequenceOracle.Count(fixture);
            foreach (int capacity in new[] { 4, 1_000 }) {
                Bounds? previous = null;
                foreach (long work in new[] { 18_000L, 400_000L, 5_000_000L }) {
                    Bounds bounds = Run(fixture, method, new(new(work)), capacity);
                    Bounds repeat = Run(fixture, method, new(new(work)), capacity);
                    Assert.That(repeat.Lower, Is.EqualTo(bounds.Lower));
                    Assert.That(repeat.Upper, Is.EqualTo(bounds.Upper));
                    Assert.That(bounds.Lower, Has.Length.EqualTo(truth.Length));
                    for (int row = 0; row < truth.Length; row++) {
                        CheckEnclosure(bounds, row, truth[row].Numerator, truth[row].Denominator);
                        if (previous is not null) {
                            Assert.That(bounds.Denominator, Is.EqualTo(previous.Denominator));
                            Assert.That(bounds.Lower[row] >= previous.Lower[row] && bounds.Upper[row] <= previous.Upper[row], Is.True);
                        }
                    }
                    previous = bounds;
                }
                if (capacity == 1_000 && scenario != "failed-removal") {
                    BigInteger tolerance = method == "continuation" ? 64 : 0;
                    Assert.That(previous!.Upper.Zip(previous.Lower).All(row => row.First - row.Second <= tolerance), Is.True,
                        $"{method} on {scenario} should converge at this capacity");
                }
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RetainedHandAndNewDrawsRemainDistinctAtLaterObservations(bool repeatedObserver) {
        DeckEntry Reveal(string id) => new(id, 1, new(ActivationLimit: null) { RandomDiscardReveal = true }) { RevealIdentity = id };
        List<DeckEntry> deck = [Reveal("first"), Reveal("second"), new("overlap", 1), new("b", 1),
            new("filter", 1, new(DrawCount: 2, RemovalCount: 1, RemovalEligibility: new("a"))), new("a", repeatedObserver ? 1 : 2)];
        if (repeatedObserver) {
            deck.Add(new("again", 1, new(DrawCount: 1, RemovalCount: 1, RemovalEligibility: new("a"))));
        }
        Goal[] goals = [new("both", [Requirement.InMembership("a"), Requirement.InMembership("b")], groupId: "g"),
            new("overlap", [Requirement.OfEntry("overlap")], groupId: "g"),
            new("exclude", [Requirement.InMembership("a", 0, 0), Requirement.InMembership("b")])];
        EngineCase fixture = new("strata", new(deck, 3, goals, [new("a", ["a", "overlap"]), new("b", ["b", "overlap"])], ["g"]),
            new(activationOrder: ActivationOrder.Priority, priorityEntryIds: ["filter", "second", "first"]));
        RevealSequenceOracle.Rational[] truth = RevealSequenceOracle.Count(fixture);
        WorkBudget budget = new(new(5_000_000));
        PostDrawCalculation calculation = Prepare(fixture, budget, out PreparedCalculation input);
        PostDrawCalculation.AnonymousResult result = calculation.ResearchAnonymous(input.Combos, input.Groups, 1_000, retainedStratum: true);
        Assert.That(result.RetainedDiscards, Is.GreaterThan(0));
        Assert.That(result.RetainedReveals, Is.GreaterThan(0));
        if (repeatedObserver) {
            Assert.That(result.RetainedObserverReveals, Is.GreaterThan(0));
        }
        else {
            Assert.That(result.JointStratumReveals, Is.GreaterThan(0));
            Assert.That(result.EmptyRetainedReveals, Is.GreaterThan(0));
        }
        Assert.That(result.Resolved, Is.EqualTo(result.Denominator));
        for (int row = 0; row < truth.Length; row++) {
            Assert.That(result.Wins[row] * truth[row].Denominator, Is.EqualTo(truth[row].Numerator * result.Denominator));
        }
    }

    [TestCase("anonymous")]
    [TestCase("batch")]
    [TestCase("strata")]
    public void InterruptionBeforeLastTerminalCommitKeepsItsMassUnresolved(string method) {
        EngineCase fixture = RandomDiscardRevealTest.SmallCase("fetch-reveal");
        RevealSequenceOracle.Rational[] truth = RevealSequenceOracle.Count(fixture);
        WorkBudget full = new(Core.CalculationWorkPolicy.Unlimited);
        Bounds complete = Run(fixture, method, full, 1_000);
        Assert.That(complete.Upper, Is.EqualTo(complete.Lower));
        long lo = 1;
        long hi = full.Spent;
        while (lo < hi) {
            long middle = lo + (hi - lo) / 2;
            Bounds? trial = TryRun(middle);
            if (trial is not null && trial.Upper.SequenceEqual(trial.Lower)) {
                hi = middle;
            }
            else {
                lo = middle + 1;
            }
        }
        Bounds interrupted = TryRun(lo - 1)!;
        Assert.That(interrupted, Is.Not.Null);
        Assert.That(interrupted.Stop, Is.EqualTo("Work"));
        Assert.That(interrupted.Upper[0] > interrupted.Lower[0], Is.True);
        for (int row = 0; row < truth.Length; row++) {
            CheckEnclosure(interrupted, row, truth[row].Numerator, truth[row].Denominator);
        }
        Bounds? TryRun(long work) {
            try {
                return Run(fixture, method, new(new(work)), 1_000);
            }
            catch (Core.ProbabilityCalculationLimitException) {
                return null; // The budget ran out before the first partition was established.
            }
        }
    }

    [Test]
    public void ReplayFrozenCase() {
        string name = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_CASE") ?? "heavy-40";
        string method = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_METHOD") ?? "strata";
        long work = long.Parse(Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_WORK") ?? "149000000");
        int capacity = int.Parse(Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_CAPACITY") ?? "2048");
        FrozenRevealCase[] cases = name == "all" ? FrozenRevealCase.ReadAll() : [FrozenRevealCase.Load(name)];
        foreach (FrozenRevealCase frozen in cases) {
            Replay(frozen, method, work, capacity);
        }
    }

    private static void Replay(FrozenRevealCase frozen, string method, long work, int capacity) {
        WorkBudget budget = new(new(work));
        Bounds result = Run(frozen.Create(CalculationMode.Fast), method, budget, capacity);
        TestContext.Out.WriteLine(JsonSerializer.Serialize(new {
            frozen.Name, method, capacity, work, budget.Spent, budget.PeakEntries, budget.PeakCells, result.Stop, result.FinalizationWork,
            Denominator = result.Denominator.ToString(),
            Lower = result.Lower.Select(value => value.ToString()),
            Upper = result.Upper.Select(value => value.ToString())
        }));
        for (int row = 0; row < result.Lower.Length; row++) {
            Assert.That(result.Lower[row] >= 0 && result.Lower[row] <= result.Upper[row] && result.Upper[row] <= result.Denominator, Is.True);
            if (frozen.Reference is { } reference) {
                string[] parts = reference[row].Split('/');
                CheckEnclosure(result, row, BigInteger.Parse(parts[0]), BigInteger.Parse(parts[1]));
            }
        }
    }

    private static Bounds Run(EngineCase fixture, string method, WorkBudget budget, int capacity) {
        PostDrawCalculation calculation = Prepare(fixture, budget, out PreparedCalculation input);
        if (method == "continuation") {
            PostDrawCalculation.ContinuationResult result = calculation.ResearchContinuation(input.Combos, input.Groups, capacity);
            return new(result.Lower.Select(value => (BigInteger)value).ToArray(), result.Upper.Select(value => (BigInteger)value).ToArray(),
                result.Scale, result.FinalizationWork, result.Stop);
        }
        if (method == "beam") {
            PostDrawCalculation.BeamResult result = calculation.ResearchBeam(input.Combos, input.Groups, capacity);
            return FromResolved(result.Wins, result.Resolved, result.Denominator, result.FinalizationWork, result.Stop);
        }
        if (method is not ("anonymous" or "batch" or "strata")) {
            throw new ArgumentException($"Unknown research method '{method}'.");
        }
        PostDrawCalculation.AnonymousResult anonymous = calculation.ResearchAnonymous(input.Combos, input.Groups, capacity,
            wholeBatches: method == "batch", retainedStratum: method == "strata");
        return FromResolved(anonymous.Wins, anonymous.Resolved, anonymous.Denominator, anonymous.FinalizationWork, anonymous.Stop);
    }

    private static PostDrawCalculation Prepare(EngineCase fixture, WorkBudget budget, out PreparedCalculation input) {
        input = fixture.Prepare(budget);
        DrawActivationPriority priority = new(fixture.Options.ActivationOrder == ActivationOrder.Priority
            ? DrawActivationMode.CustomPriority : DrawActivationMode.EncounterOrder, fixture.Options.PriorityEntryIds);
        return new(input.Cards, fixture.Request.HandSize, budget, priority);
    }

    private static Bounds FromResolved(BigInteger[] wins, BigInteger resolved, BigInteger denominator, long finalizationWork, string stop) =>
        new(wins, wins.Select(win => win + (denominator - resolved)).ToArray(), denominator, finalizationWork, stop);

    private static void CheckEnclosure(Bounds bounds, int row, BigInteger numerator, BigInteger denominator) {
        Assert.That(bounds.Lower[row] >= 0 && bounds.Upper[row] <= bounds.Denominator, Is.True, $"row {row}");
        BigInteger target = numerator * bounds.Denominator;
        Assert.That(bounds.Lower[row] * denominator <= target && target <= bounds.Upper[row] * denominator, Is.True, $"row {row}");
    }
}
