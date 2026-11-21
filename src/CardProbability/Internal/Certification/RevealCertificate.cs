using System.Numerics;
using CardProbability.Internal.Contracts;
using CardProbability.Internal.Counting;
using CardProbability.Internal.Model;
using CardProbability.Internal.Resources;

namespace CardProbability.Internal.Certification;

internal static class RevealCertificate {
    internal static ProbabilityCalculationResult Complete(
        BigInteger[] wins, BigInteger resolved, BigInteger denominator,
        List<Combo> combos, IReadOnlyList<ComboGroup>? groups, bool unionOnly, WorkBudget budget, CalculationModel mode = CalculationModel.Fast
    ) {
        CalculationResultRows rows = new(combos, groups);
        if (wins.Length != rows.Count || denominator.Sign <= 0 || resolved.Sign < 0 || resolved > denominator) {
            throw new InvalidOperationException("Invalid resolved mass in reveal certificate.");
        }
        // All rows and their unions share this partition of physical histories.
        // Every unresolved history can contribute at most once to any given row.
        BigInteger unresolved = denominator - resolved;
        if (mode == CalculationModel.Exact && !unresolved.IsZero) {
            throw new InvalidOperationException("An Exact reveal result requires complete physical coverage.");
        }
        int count = unionOnly ? 1 : rows.Count;
        long limbs = 1 + WorkBudget.IntegerCells(denominator);
        budget.CheckStorage(count, count * (6 * limbs + 4));
        budget.Spend(count * (4 * limbs * limbs + 4));
        ExactProbability[] upper = new ExactProbability[count];
        SearchAvailabilityInterval[] intervals = new SearchAvailabilityInterval[count];
        for (int i = 0; i < count; i++) {
            if (wins[i].Sign < 0 || wins[i] > resolved) {
                throw new InvalidOperationException("Winning mass exceeds resolved mass in reveal certificate.");
            }
            upper[i] = new(wins[i] + unresolved, denominator);
            string label = i == 0 ? "Any active combo" : i <= combos.Count ? combos[i - 1].Name ?? $"Unnamed combo {i}" : groups![i - 1 - combos.Count].Name;
            intervals[i] = new(label, new ExactProbability(wins[i], denominator).ToFraction(), upper[i].ToFraction());
        }

        bool exact = unresolved.IsZero;
        string detail = exact ? "All physical reveal and discard histories were counted under the fixed activation policy."
            : "Lower bounds count resolved winning histories. Upper bounds also include the unresolved physical probability mass.";
        SearchAvailabilityCertificate certificate = new(exact ? SearchAvailabilityStatus.Exact : SearchAvailabilityStatus.Bounded, detail,
            intervals[0], [.. intervals.Skip(1).Take(combos.Count)], [.. intervals.Skip(1 + combos.Count)]);
        CalculationFractions primary = new(intervals[0].Upper, [.. certificate.Combos.Select(row => row.Upper)], [.. certificate.Groups.Select(row => row.Upper)]);
        CalculationResultContract contract = new(CalculationResultContract.CurrentVersion, mode, mode,
            CalculationResultContract.EffectsReference, CalculationRuleScope.SavedEffectsFixedPolicy, CalculationCompletion.Success,
            exact ? CalculationAccuracy.Exact : CalculationAccuracy.Bounded, detail, "OrderedReveal", exact ? "PhysicalRevealHistories" : null, primary);
        ProbabilityCalculationResult result = unionOnly ? new(upper[0].ToDouble(budget), [], []) : rows.ToResult(upper).ToPublic(combos, groups, budget);
        return result with { Contract = contract, SearchAvailability = mode == CalculationModel.Exact ? null : certificate };
    }
}
