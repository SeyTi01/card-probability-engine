using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using CardProbability.Internal;
using CardProbability.Internal.Counting;
using CardProbability.Internal.DrawResolution;
using CardProbability.Internal.Resources;
using C = CardProbability.Internal.Contracts;
using M = CardProbability.Internal.Model;

namespace CardProbability;

internal static class OriginalStrata {
    internal static CalculationResult Calculate(CalculationRequest request, CalculationOptions options) {
        int capacity = int.Parse(Environment.GetEnvironmentVariable("REVEAL_REFERENCE_CAPACITY") ?? "1500000");
        WorkBudget budget = new(C.CalculationWorkPolicy.Unlimited, new C.CalculationStoragePolicy(capacity, 160_000_000));
        PreparedCalculation input = PreparedCalculation.Create(request, options, budget);
        M.DrawActivationPriority priority = new(options.ActivationOrder == ActivationOrder.Priority
            ? M.DrawActivationMode.CustomPriority : M.DrawActivationMode.EncounterOrder, options.PriorityEntryIds);
        PostDrawCalculation engine = new(input.Cards, request.HandSize, budget, priority);
        PostDrawCalculation.AnonymousResult result = engine.ResearchAnonymous(input.Combos, input.Groups, capacity, retainedStratum: true);
        Console.WriteLine(JsonSerializer.Serialize(new { result.Stop, result.Expanded, result.Evicted, budget.Spent,
            budget.PeakEntries, budget.PeakCells, Resolved = result.Resolved.ToString(), Denominator = result.Denominator.ToString() }));
        if (result.Resolved != result.Denominator || result.Evicted != 0 || result.Stop != "exhausted retained frontier") {
            throw new InvalidOperationException("Original retained-stratum replay left unresolved physical mass.");
        }
        ProbabilityValue[] values = result.Wins.Select(win => {
            ExactProbability ratio = new(win, result.Denominator);
            C.ProbabilityFraction fraction = ratio.ToFraction();
            RationalProbability primary = new(fraction.Numerator, fraction.Denominator);
            return new ProbabilityValue(ratio.ToDouble(budget), primary, ProbabilityAccuracy.Exact, new(primary, primary));
        }).ToArray();
        return new(CalculationStatus.Completed, options.Mode, ModelScope.FixedOrderEffects, "Exhausted original retained-stratum frontier.",
            ProbabilityAccuracy.Exact, values[0],
            request.Goals.Select((goal,i) => new GoalProbability(goal.Id, values[1+i])).ToImmutableArray(),
            request.Groups.Select((id,i) => new GroupProbability(id, request.Goals.Count(goal => goal.GroupId == id), values[1+request.Goals.Length+i])).ToImmutableArray(),
            new(budget.Spent, budget.Remaining, budget.PeakEntries, budget.PeakCells));
    }
}
