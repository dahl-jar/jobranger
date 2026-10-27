using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Messaging.Limits;

namespace Jobranger.Messaging.Tests.Limits;

public sealed partial class MessageLimitsTests {
    private const string payloadSchema = "payload.schema.json";
    private const string envelopeSchema = "envelope.schema.json";
    private const int base64Quantum = 4;
    private const int bytesPerQuantum = 3;

    private static readonly string testData = Path.Combine(AppContext.BaseDirectory, "TestData");

    public static TheoryData<int, string, string> SchemaLimits => new() {
        { Base64Chars(MessageLimits.ChunkDataMaxBytes), payloadSchema, "/$defs/Chunk/properties/data/maxLength" },
        { MessageLimits.ChunkMaxCount, payloadSchema, "/$defs/Chunk/properties/count/maximum" },
        { MessageLimits.ChunkMaxCount - 1, payloadSchema, "/$defs/Chunk/properties/index/maximum" },
        { MessageLimits.CvMaxBytes, payloadSchema, "/$defs/CvUpload/properties/size_bytes/maximum" },
        { Base64Chars(MessageLimits.CvMaxBytes), payloadSchema, "/$defs/CvUpload/properties/content_b64/maxLength" },
        { Base64Chars(MessageLimits.CvMaxBytes), payloadSchema, "/$defs/ContextUpload/properties/content_b64/maxLength" },
        { MessageLimits.UrlMaxChars, payloadSchema, "/$defs/Url/maxLength" },
        { MessageLimits.GuidanceLimit, payloadSchema, "/$defs/GuidanceSnapshot/properties/text/maxLength" },
        { MessageLimits.GuidanceLimit, payloadSchema, "/$defs/GuidanceUpdate/properties/text/maxLength" },
        { MessageLimits.AboutLimit, payloadSchema, "/$defs/ContextSnapshot/properties/about/maxLength" },
        { MessageLimits.AboutLimit, payloadSchema, "/$defs/ContextUpdate/properties/about/maxLength" },
        { MessageLimits.DocsLimit, payloadSchema, "/$defs/ContextSnapshot/properties/docs/maxItems" },
        { MessageLimits.CiphertextB64MaxChars, envelopeSchema, "/$defs/EnvelopeData/properties/ciphertext/maxLength" },
        { Base64UrlChars(MessageLimits.GcmTagBytes), envelopeSchema, "/$defs/EnvelopeData/properties/ciphertext/minLength" },
        { Base64UrlChars(MessageLimits.HelloMaxBytes), envelopeSchema, "/$defs/EnvelopeHello/properties/ciphertext/maxLength" },
    };

    [Fact]
    public void ShouldEqualFrontendLimits_WhenComparedByName() {
        string text = File.ReadAllText(Path.Combine(testData, "Frontend", "limits.ts"));
        SortedDictionary<string, string> frontend = new(
            FrontendLimit().Matches(text).ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value),
            StringComparer.Ordinal);

        SortedDictionary<string, string> contract = new(
            typeof(MessageLimits)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .ToDictionary(field => ScreamingSnake(field.Name), ConstantText),
            StringComparer.Ordinal);

        Assert.Equal(frontend, contract);
    }

    [Theory]
    [MemberData(nameof(SchemaLimits))]
    public void ShouldEqualSchemaLimit_WhenSchemaDeclaresIt(int limit, string schema, string path) {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(Path.Combine(testData, "Messages", schema)));

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
    private static partial Regex FrontendLimit();

    [GeneratedRegex("(?<!^)(?=[A-Z])")]
    private static partial Regex WordStart();
}
