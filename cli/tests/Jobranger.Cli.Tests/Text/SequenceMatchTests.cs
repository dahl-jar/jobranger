using System.Text.Json;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class SequenceMatchTests {
    private const double tolerance = 1e-12;

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
}
