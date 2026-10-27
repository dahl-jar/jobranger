using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Contracts.Limits;

namespace Jobranger.Contracts.Tests.Limits;

public sealed partial class ContractLimitsTests {
    private const string payloadSchema = "payload.schema.json";
    private const string envelopeSchema = "envelope.schema.json";
    private const int base64Quantum = 4;
    private const int bytesPerQuantum = 3;

    private static readonly string testData = Path.Combine(AppContext.BaseDirectory, "TestData");

    public static TheoryData<int, string, string> SchemaLimits => new() {
        { Base64Chars(ContractLimits.ChunkDataMaxBytes), payloadSchema, "/$defs/Chunk/properties/data/maxLength" },
        { ContractLimits.ChunkMaxCount, payloadSchema, "/$defs/Chunk/properties/count/maximum" },
        { ContractLimits.ChunkMaxCount - 1, payloadSchema, "/$defs/Chunk/properties/index/maximum" },
        { ContractLimits.CvMaxBytes, payloadSchema, "/$defs/CvUpload/properties/size_bytes/maximum" },
        { Base64Chars(ContractLimits.CvMaxBytes), payloadSchema, "/$defs/CvUpload/properties/content_b64/maxLength" },
        { Base64Chars(ContractLimits.CvMaxBytes), payloadSchema, "/$defs/ContextUpload/properties/content_b64/maxLength" },
        { ContractLimits.UrlMaxChars, payloadSchema, "/$defs/Url/maxLength" },
        { ContractLimits.GuidanceLimit, payloadSchema, "/$defs/GuidanceSnapshot/properties/text/maxLength" },
        { ContractLimits.GuidanceLimit, payloadSchema, "/$defs/GuidanceUpdate/properties/text/maxLength" },
        { ContractLimits.AboutLimit, payloadSchema, "/$defs/ContextSnapshot/properties/about/maxLength" },
        { ContractLimits.AboutLimit, payloadSchema, "/$defs/ContextUpdate/properties/about/maxLength" },
        { ContractLimits.DocsLimit, payloadSchema, "/$defs/ContextSnapshot/properties/docs/maxItems" },
        { ContractLimits.CiphertextB64MaxChars, envelopeSchema, "/$defs/EnvelopeData/properties/ciphertext/maxLength" },
        { Base64UrlChars(ContractLimits.GcmTagBytes), envelopeSchema, "/$defs/EnvelopeData/properties/ciphertext/minLength" },
        { Base64UrlChars(ContractLimits.HelloMaxBytes), envelopeSchema, "/$defs/EnvelopeHello/properties/ciphertext/maxLength" },
    };

    [Fact]
    public void ShouldEqualWebLimits_WhenComparedByName() {
        string text = File.ReadAllText(Path.Combine(testData, "Web", "limits.ts"));
        SortedDictionary<string, string> web = new(
            WebLimit().Matches(text).ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value),
            StringComparer.Ordinal);

        SortedDictionary<string, string> contract = new(
            typeof(ContractLimits)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .ToDictionary(field => ScreamingSnake(field.Name), ConstantText),
            StringComparer.Ordinal);

        Assert.Equal(web, contract);
    }

    [Theory]
    [MemberData(nameof(SchemaLimits))]
    public void ShouldEqualSchemaLimit_WhenSchemaDeclaresIt(int limit, string schema, string path) {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(Path.Combine(testData, "Contracts", schema)));

        int declared = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(node, (parent, key) => parent?[key])!
            .GetValue<int>();

        Assert.Equal(declared, limit);
    }

    private static int Base64Chars(int bytes) => (bytes + bytesPerQuantum - 1) / bytesPerQuantum * base64Quantum;

    private static int Base64UrlChars(int bytes) => ((bytes * base64Quantum) + bytesPerQuantum - 1) / bytesPerQuantum;

    private static string ScreamingSnake(string name) => WordStart().Replace(name, "_").ToUpperInvariant();

    private static string ConstantText(FieldInfo field) =>
        Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)!;

    [GeneratedRegex(@"^export const (?<name>[A-Z][A-Z0-9_]*) = (?:'(?<value>[^']*)'|(?<value>[0-9]+))$", RegexOptions.Multiline)]
    private static partial Regex WebLimit();

    [GeneratedRegex("(?<!^)(?=[A-Z])")]
    private static partial Regex WordStart();
}
