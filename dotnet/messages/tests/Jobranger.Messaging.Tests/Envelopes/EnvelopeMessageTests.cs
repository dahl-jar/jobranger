using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Messaging.Envelopes;
using Jobranger.Messaging.Limits;

namespace Jobranger.Messaging.Tests.Envelopes;

public sealed class EnvelopeMessageTests {
    private static readonly string messages = Path.Combine(AppContext.BaseDirectory, "TestData", "Messages");
    private static readonly string goldenFolder = Path.Combine(messages, "fixtures", "envelope");
    private static readonly string invalidFolder = Path.Combine(messages, "fixtures", "envelope-invalid");
    private const string missingPropertyPattern = "*-without-*.json";
    private const long maxSeq = 4294967295;
    private const int dataCiphertextMinChars = 22;
    private const int helloCiphertextMinChars = 4;
    private const int helloCiphertextMaxChars = 5462;
    private const byte overlongLeadByte = 0xC0;

    public static TheoryData<string> GoldenFixtures => FileNames(goldenFolder, "*.json");

    public static TheoryData<string> MissingPropertyFixtures => FileNames(invalidFolder, missingPropertyPattern);

    public static TheoryData<string> InvalidFixtures => FileNames(invalidFolder, "*.json");

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ShouldRoundTripEnvelopeFixture(string fileName) {
        string json = File.ReadAllText(Path.Combine(goldenFolder, fileName));
        Envelope? envelope = JsonSerializer.Deserialize(json, MessagesJsonContext.Default.Envelope);

        string serialized = JsonSerializer.Serialize(envelope!, MessagesJsonContext.Default.Envelope);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serialized)), serialized);
    }

    [Fact]
    public void ShouldCoverEverySchemaProperty() {
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(messages, "envelope.schema.json")))!;
        IEnumerable<string> schemaNames = schema["oneOf"]!.AsArray()
            .Select(reference => reference!["$ref"]!.GetValue<string>().Split('/')[^1])
            .SelectMany(name => schema["$defs"]![name]!["properties"]!.AsObject().Select(property => property.Key))
            .Distinct()
            .Order();

        IEnumerable<string> recordNames = MessagesJsonContext.Default.Envelope.Properties
            .Select(property => property.Name)
            .Order();

        Assert.Equal(schemaNames, recordNames);
    }

    [Theory]
    [MemberData(nameof(MissingPropertyFixtures))]
    public void ShouldRejectEnvelopeWithoutRequiredProperty(string fileName) {
        string json = File.ReadAllText(Path.Combine(invalidFolder, fileName));

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, MessagesJsonContext.Default.Envelope));
    }

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ShouldAcceptGoldenEnvelope(string fileName) {
        byte[] json = File.ReadAllBytes(Path.Combine(goldenFolder, fileName));

        bool accepted = EnvelopeRules.TryParse(json, out Envelope? envelope);

        Assert.True(accepted);
        Assert.NotNull(envelope);
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void ShouldRejectInvalidEnvelope(string fileName) {
        byte[] json = File.ReadAllBytes(Path.Combine(invalidFolder, fileName));

        Assert.False(EnvelopeRules.TryParse(json, out _));
    }

    [Fact]
    public void ShouldFindEveryInvalidFixture() {
        Assert.Equal(38, InvalidFixtures.Count);
    }

    [Theory]
    [InlineData("\"kind\":\"presence\",")]
    [InlineData("\"extra\":\"\\ud800\",")]
    [InlineData("\"\\ud800\":1,")]
    [InlineData("\"extra\":1e400,")]
    public void ShouldRejectEnvelopeWithUnsafeJson(string extra) {
        byte[] json = WithExtra(extra);

        Assert.False(EnvelopeRules.TryParse(json, out _));
    }

    [Fact]
    public void ShouldRejectEnvelopeWithInvalidUtf8() {
        byte[] json = WithExtra("\"extra\":\"x\",");
        json[json.AsSpan().IndexOf("\"x\""u8) + 1] = overlongLeadByte;

        Assert.False(EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData(MessageLimits.MaxJsonDepth - 1, true)]
    [InlineData(MessageLimits.MaxJsonDepth, false)]
    public void ShouldLimitNestingDepth(int arrays, bool accepted) {
        byte[] json = WithExtra($"\"extra\":{new string('[', arrays)}{new string(']', arrays)},");

        Assert.Equal(accepted, EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData("-", MessageLimits.MaxIntegerDigits, true)]
    [InlineData("-", MessageLimits.MaxIntegerDigits + 1, false)]
    [InlineData("", MessageLimits.MaxIntegerDigits + 1, false)]
    [InlineData("0.", MessageLimits.MaxIntegerDigits + 1, true)]
    public void ShouldLimitIntegerDigits(string prefix, int digits, bool accepted) {
        byte[] json = WithExtra($"\"extra\":{prefix}{new string('9', digits)},");

        Assert.Equal(accepted, EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(maxSeq)]
    public void ShouldAcceptDataSeqAtBounds(long seq) {
        string json = File.ReadAllText(Path.Combine(goldenFolder, "data.json"));
        string bounded = json.Replace("\"seq\": 1,", $"\"seq\": {seq},", StringComparison.Ordinal);

        Assert.True(EnvelopeRules.TryParse(Encoding.UTF8.GetBytes(bounded), out _));
    }

    [Theory]
    [InlineData("data.json", dataCiphertextMinChars)]
    [InlineData("data.json", MessageLimits.CiphertextB64MaxChars)]
    [InlineData("hello.json", helloCiphertextMinChars)]
    [InlineData("hello.json", helloCiphertextMaxChars)]
    public void ShouldAcceptCiphertextAtBounds(string fileName, int length) {
        JsonNode envelope = JsonNode.Parse(File.ReadAllText(Path.Combine(goldenFolder, fileName)))!;
        envelope["ciphertext"] = new string('A', length);

        Assert.True(EnvelopeRules.TryParse(Encoding.UTF8.GetBytes(envelope.ToJsonString()), out _));
    }

    private static byte[] WithExtra(string extra) {
        string json = File.ReadAllText(Path.Combine(goldenFolder, "data.json"));
        return Encoding.UTF8.GetBytes(json.Replace("\"v\": 1,", "\"v\": 1, " + extra, StringComparison.Ordinal));
    }

    private static TheoryData<string> FileNames(string folder, string pattern) {
        IEnumerable<string> names = Directory.GetFiles(folder, pattern)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order();
        return new TheoryData<string>(names);
    }
}
