using System.Globalization;
using System.Text.Json.Nodes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class LaxGolden {
    private const string folder = "Lax";
    private const string infinity = "inf";
    private const string negativeInfinity = "-inf";
    private const string notANumber = "nan";

    public static JsonArray Rows(string fileName) =>
        JsonNode.Parse(MatchingFixtures.GoldenText(folder, fileName))!.AsArray();

    public static TheoryData<int> Indexes(JsonArray rows) => [.. Enumerable.Range(0, rows.Count)];

    public static long? ReprBits(string? pythonRepr) => pythonRepr switch {
        null => null,
        infinity => Bits(double.PositiveInfinity),
        negativeInfinity => Bits(double.NegativeInfinity),
        notANumber => Bits(double.NaN),
        _ => Bits(double.Parse(pythonRepr, NumberStyles.Float, CultureInfo.InvariantCulture)),
    };

    public static long? Bits(double? value) => value switch {
        null => null,
        double number when double.IsNaN(number) => BitConverter.DoubleToInt64Bits(double.NaN),
        double number => BitConverter.DoubleToInt64Bits(number),
    };
}
