using System.Numerics;
using CardProbability.EngineTests.Fixtures;

namespace CardProbability.EngineTests;

[TestFixture]
public sealed class FrozenRevealReplayTest {
    [TestCase("control-40")]
    [TestCase("single-40")]
    [TestCase("mixed-40")]
    [TestCase("control-60")]
    [TestCase("single-60")]
    [TestCase("mixed-60")]
    [TestCase("challenge-mixed-40-direct-exclusion")]
    public void CompletedRowsMatchOriginalFractions(string name) {
        CheckExactRows(name, CalculationMode.Exact);
        CheckExactRows(name, CalculationMode.Fast);
    }

    [TestCase("heavy-40")]
    [TestCase("heavy-60")]
    [TestCase("challenge-heavy-40-hand6")]
    [TestCase("challenge-heavy-60-priority")]
    [TestCase("challenge-heavy-40-multiplicity")]
    [TestCase("challenge-heavy-60-direct")]
    public void HeavyFastCertifiesEveryRowWithinTwoPercentagePoints(string name) {
        FrozenRevealCase frozen = FrozenRevealCase.Load(name);
        EngineCase fixture = frozen.Create(CalculationMode.Fast);
        CalculationResult result = CalculationEngine.Calculate(fixture.Request, fixture.Options);
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
        Assert.That(result.Metrics.WorkSpent, Is.LessThanOrEqualTo(fixture.Options.Policy.WorkUnits!.Value));
        Assert.That(result.Metrics.PeakEntries, Is.LessThanOrEqualTo(fixture.Options.Policy.EntryLimit));
        Assert.That(result.Metrics.PeakCells, Is.LessThanOrEqualTo(fixture.Options.Policy.CellLimit));
        ProbabilityValue[] rows = [result.Total!, .. result.Goals.Select(goal => goal.Value), .. result.Groups.Select(group => group.Value)];
        Assert.That(rows, Has.Length.EqualTo(frozen.Reference.Length));
        for (int row = 0; row < rows.Length; row++) {
            CertifiedInterval? interval = rows[row].Certified;
            Assert.That(interval, Is.Not.Null);
            Assert.That(rows[row].Primary, Is.EqualTo(interval!.Upper));
            string[] fraction = frozen.Reference[row].Split('/');
            BigInteger n = BigInteger.Parse(fraction[0]);
            BigInteger d = BigInteger.Parse(fraction[1]);
            BigInteger lower = BigInteger.Parse(interval.Lower.Numerator);
            BigInteger lowerD = BigInteger.Parse(interval.Lower.Denominator);
            BigInteger upper = BigInteger.Parse(interval.Upper.Numerator);
            BigInteger upperD = BigInteger.Parse(interval.Upper.Denominator);
            Assert.That(lower * d <= n * lowerD && n * upperD <= upper * d, Is.True, $"row {row} must enclose {n}/{d}");
            Assert.That(50 * (upper * lowerD - lower * upperD) <= upperD * lowerD, Is.True, $"row {row} exceeds 2 percentage points");
        }
    }

    [TestCase("heavy-40")]
    [TestCase("heavy-60")]
    [TestCase("challenge-heavy-40-hand6")]
    public void HeavyExactMatchesPhysicalFractions(string name) => CheckExactRows(name, CalculationMode.Exact);

    private static void CheckExactRows(string name, CalculationMode mode) {
        FrozenRevealCase frozen = FrozenRevealCase.Load(name);
        EngineCase fixture = frozen.Create(mode);
        CalculationResult result = CalculationEngine.Calculate(fixture.Request, fixture.Options);
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
        ProbabilityValue[] rows = [result.Total!, .. result.Goals.Select(goal => goal.Value), .. result.Groups.Select(group => group.Value)];
        Assert.That(rows.Select(row => $"{row.Primary.Numerator}/{row.Primary.Denominator}"), Is.EqualTo(frozen.Reference));
        Assert.That(rows.All(row => row.Accuracy == ProbabilityAccuracy.Exact && row.Certified == new CertifiedInterval(row.Primary, row.Primary)), Is.True);
    }
}
