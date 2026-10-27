using System.Text.Json.Nodes;
using Jobranger.Contracts.Fields;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Tests.Fields;

public sealed class UrlParityTests {
    private const string rowsKey = "rows";
    private const string urlKey = "url";
    private const string encodedKey = "encoded";

    private static readonly string parityFile = Path.Combine(AppContext.BaseDirectory, "TestData", "Contracts", "fixtures", "urls", "parity.json");

    public static TheoryData<string, string?> PythonRows {
        get {
            TheoryData<string, string?> rows = [];
            foreach (JsonNode? row in JsonNode.Parse(File.ReadAllText(parityFile))![rowsKey]!.AsArray()) {
                rows.Add(row![urlKey]!.GetValue<string>(), row[encodedKey]?.GetValue<string>());
            }
            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(PythonRows))]
    public void ShouldEncodeLikePython_WhenUrlIsInParityTable(string url, string? expected) {
        string? actual = Encoded(url);

        Assert.Equal(expected, actual);
    }

    private static string? Encoded(string url) {
        try {
            return Urls.Encode(url);
        } catch (ContractException) {
            return null;
        }
    }
}
