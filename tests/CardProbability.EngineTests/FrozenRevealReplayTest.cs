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
    public void HeavyFastReturnsExactRowsOrAWorkLimit(string name) {
        FrozenRevealCase frozen = FrozenRevealCase.Load(name);
        EngineCase fixture = frozen.Create(CalculationMode.Fast);
        CalculationResult result = CalculationEngine.Calculate(fixture.Request, fixture.Options);
        if (result.Status == CalculationStatus.Completed) {
            AssertRows(result, frozen);
            return;
        }
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.WorkLimit));
        Assert.That(result.Total, Is.Null);
        Assert.That(result.Goals, Is.Empty);
        Assert.That(result.Groups, Is.Empty);
    }

    [TestCase("heavy-40")]
    [TestCase("heavy-60")]
    [Explicit("Full physical replay at standard storage; each case can take several minutes.")]
    [NonParallelizable]
    public void ReplayHeavyExact(string name) => CheckExactRows(name, CalculationMode.Exact);

    private static void CheckExactRows(string name, CalculationMode mode) {
        FrozenRevealCase frozen = FrozenRevealCase.Load(name);
        EngineCase fixture = frozen.Create(mode);
        CalculationResult result = CalculationEngine.Calculate(fixture.Request, fixture.Options);
        AssertRows(result, frozen);
        TestContext.Out.WriteLine($"{name} {mode}, work {result.Metrics.WorkSpent}, peak {result.Metrics.PeakEntries} entries, {result.Metrics.PeakCells} cells");
    }

    private static void AssertRows(CalculationResult result, FrozenRevealCase frozen) {
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
        ProbabilityValue[] rows = [result.Total!, .. result.Goals.Select(goal => goal.Value), .. result.Groups.Select(group => group.Value)];
        Assert.That(rows.Select(row => $"{row.Primary.Numerator}/{row.Primary.Denominator}"), Is.EqualTo(frozen.Reference));
        Assert.That(rows.All(row => row.Accuracy == ProbabilityAccuracy.Exact && row.Certified == new CertifiedInterval(row.Primary, row.Primary)), Is.True);
    }
}
