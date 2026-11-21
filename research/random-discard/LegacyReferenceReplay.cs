using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardProbability.EngineTests.Fixtures;

namespace CardProbability.EngineTests;

// This harness uses public execution and the frozen fixture translator shared by both revisions.
[TestFixture, Explicit("Opt-in public reveal replay"), NonParallelizable]
public sealed class LegacyReferenceReplay {
    [Test]
    public void ExportExactRows() {
        string name = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_CASE")
            ?? throw new ArgumentException("Set CARD_PROBABILITY_REVEAL_CASE to a frozen fixture name.");
        string output = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_OUTPUT")
            ?? throw new ArgumentException("Set CARD_PROBABILITY_REVEAL_OUTPUT to an output directory.");
        FrozenRevealCase frozen = FrozenRevealCase.Load(name);
        CalculationResult result = Replay(frozen, CalculationMode.Exact, output, $"{frozen.Name}.json");
        Assert.That(result.Status, Is.EqualTo(CalculationStatus.Completed), result.Detail);
    }

    [Test]
    public void ReplayPublicCases() {
        string name = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_CASE") ?? "all";
        string selection = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_MODE") ?? "both";
        string output = Environment.GetEnvironmentVariable("CARD_PROBABILITY_REVEAL_OUTPUT")
            ?? throw new ArgumentException("Set CARD_PROBABILITY_REVEAL_OUTPUT to an output directory.");
        CalculationMode[] modes = selection switch {
            "Fast" => [CalculationMode.Fast],
            "Exact" => [CalculationMode.Exact],
            "both" => [CalculationMode.Fast, CalculationMode.Exact],
            _ => throw new ArgumentException("Use Fast, Exact or both for CARD_PROBABILITY_REVEAL_MODE.")
        };
        FrozenRevealCase[] cases = name == "all" ? FrozenRevealCase.ReadAll() : [FrozenRevealCase.Load(name)];
        foreach (FrozenRevealCase frozen in cases) {
            foreach (CalculationMode mode in modes) {
                Replay(frozen, mode, output, $"{frozen.Name}-{mode}.json");
            }
        }
    }

    private static CalculationResult Replay(FrozenRevealCase frozen, CalculationMode mode, string output, string fileName) {
        EngineCase fixture = frozen.Create(mode);
        string[]? reference = frozen.Reference;
        long start = Stopwatch.GetTimestamp();
        CalculationResult result = CalculationEngine.Calculate(fixture.Request, fixture.Options);
        double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        ProbabilityValue[] values = result.Total is null ? []
            : [result.Total, .. result.Goals.Select(row => row.Value), .. result.Groups.Select(row => row.Value)];
        string[] rows = values.Select(value => $"{value.Primary.Numerator}/{value.Primary.Denominator}").ToArray();
        JsonSerializerOptions jsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        string json = JsonSerializer.Serialize(new {
            frozen.Name, Mode = mode, fixture.Options.Policy, Milliseconds = milliseconds, Rows = rows, Reference = reference,
            result.Metrics, Result = result
        }, jsonOptions);
        Directory.CreateDirectory(output);
        string path = Path.Combine(output, fileName);
        File.WriteAllText(path + ".pending", json + Environment.NewLine);
        File.Move(path + ".pending", path, overwrite: true);
        TestContext.Out.WriteLine(path);

        if (result.Status != CalculationStatus.Completed) {
            Assert.That(result.Status, Is.AnyOf(CalculationStatus.WorkLimit, CalculationStatus.StorageLimit), result.Detail);
            Assert.That(result.Total, Is.Null);
            Assert.That(result.Goals, Is.Empty);
            Assert.That(result.Groups, Is.Empty);
            return result;
        }
        Assert.That(result.Goals.Select(row => row.Id), Is.EqualTo(new[] { "distinct", "overlap", "exclusion" }));
        Assert.That(result.Groups.Select(row => row.Id), Is.EqualTo(new[] { "g", "h" }));
        for (int index = 0; index < values.Length; index++) {
            ProbabilityValue value = values[index];
            Assert.That(value.Certified, Is.Not.Null, $"{frozen.Name}/{mode}, row {index}");
            Assert.That(value.Primary, Is.EqualTo(value.Certified!.Upper));
            if (mode == CalculationMode.Exact || value.Accuracy == ProbabilityAccuracy.Exact) {
                Assert.That(value.Accuracy, Is.EqualTo(ProbabilityAccuracy.Exact));
                Assert.That(value.Certified, Is.EqualTo(new CertifiedInterval(value.Primary, value.Primary)));
                if (reference is not null) {
                    Assert.That(rows[index], Is.EqualTo(reference[index]), $"{frozen.Name}/{mode}, row {index}");
                }
            }
            else if (reference is not null && value.Certified is { } interval) {
                string[] parts = reference[index].Split('/');
                BigInteger n = BigInteger.Parse(parts[0]);
                BigInteger d = BigInteger.Parse(parts[1]);
                Assert.That(BigInteger.Parse(interval.Lower.Numerator) * d <= n * BigInteger.Parse(interval.Lower.Denominator)
                    && BigInteger.Parse(interval.Upper.Numerator) * d >= n * BigInteger.Parse(interval.Upper.Denominator), Is.True,
                    $"{frozen.Name}/{mode}, row {index} must enclose the frozen fraction");
            }
        }
        return result;
    }
}
