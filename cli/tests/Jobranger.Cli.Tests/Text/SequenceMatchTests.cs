using System.Text.Json;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class SequenceMatchTests {
    private const double tolerance = 1e-12;
    private const int seed = 20261025;
    private const int pairCount = 300;
    private const int maxLength = 600;
    private const int maxChanges = 40;

    private static readonly string[] alphabet = ["a", "b", "c", "d", "e", "\u00E9", " ", "\U0001F600"];
    private static readonly string tablePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Text", "sequence-match.json");

    public static TheoryData<string, string, double> PythonRatios() {
        using JsonDocument table = JsonDocument.Parse(File.ReadAllBytes(tablePath));
        return [.. table.RootElement.EnumerateArray().Select(row => (
            row.GetProperty("a").GetString()!,
            row.GetProperty("b").GetString()!,
            row.GetProperty("ratio").GetDouble()))];
    }

    [Theory]
    [MemberData(nameof(PythonRatios))]
    public void ShouldMatchPythonRatio_WhenTextsComeFromDifflibTable(string left, string right, double expected) {
        Assert.Equal(expected, SequenceMatch.Ratio(left, right), tolerance);
    }

    [Fact]
    public void ShouldMatchReferenceRatio_WhenTextsAreGenerated() {
        Random random = new(seed);
        (string Left, string Right)[] pairs = [.. Enumerable.Range(0, pairCount).Select(_ => Pair(random))];
        double[] expected = [.. pairs.Select(pair => ReferenceSequenceMatch.Ratio(pair.Left, pair.Right))];

        double[] ratios = [.. pairs.Select(pair => SequenceMatch.Ratio(pair.Left, pair.Right))];

        Assert.Equal(expected, ratios);
    }

    private static (string Left, string Right) Pair(Random random) {
        string[] left = Generated(random);
        string[] right = random.Next(4) == 0 ? Generated(random) : [.. left];
        for (int change = random.Next(maxChanges); change > 0 && right.Length > 0; change--) {
            right[random.Next(right.Length)] = alphabet[random.Next(alphabet.Length)];
        }
        return (string.Concat(left), string.Concat(right));
    }

    private static string[] Generated(Random random) =>
        [.. Enumerable.Range(0, random.Next(maxLength)).Select(_ => alphabet[random.Next(alphabet.Length)])];
}
