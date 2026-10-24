using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Jobranger.Relay.Core.Tests;

public sealed class EnvelopeContractTests
{
    private static readonly string contracts = Path.Combine(AppContext.BaseDirectory, "contracts");
    private static readonly string goldenFolder = Path.Combine(contracts, "fixtures", "envelope");
    private static readonly string invalidFolder = Path.Combine(contracts, "fixtures", "envelope-invalid");
    private const string missingPropertyPattern = "*-without-*.json";
    private const long maxSeq = 9007199254740991;

    public static TheoryData<string> GoldenFixtures => FileNames(goldenFolder, "*.json");

    public static TheoryData<string> MissingPropertyFixtures => FileNames(invalidFolder, missingPropertyPattern);

    public static TheoryData<string> InvalidFixtures => FileNames(invalidFolder, "*.json");

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ShouldRoundTripEnvelopeFixture(string fileName)
    {
        string json = File.ReadAllText(Path.Combine(goldenFolder, fileName));
        Envelope? envelope = JsonSerializer.Deserialize(json, EnvelopeJsonContext.Default.Envelope);

        string serialized = JsonSerializer.Serialize(envelope!, EnvelopeJsonContext.Default.Envelope);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serialized)), serialized);
    }

    [Fact]
    public void ShouldCoverEverySchemaProperty()
    {
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(contracts, "envelope.schema.json")))!;
        IEnumerable<string> schemaNames = schema["oneOf"]!.AsArray()
            .Select(reference => reference!["$ref"]!.GetValue<string>().Split('/')[^1])
            .SelectMany(name => schema["$defs"]![name]!["properties"]!.AsObject().Select(property => property.Key))
            .Distinct()
            .Order();

        IEnumerable<string> recordNames = EnvelopeJsonContext.Default.Envelope.Properties
            .Select(property => property.Name)
            .Order();

        Assert.Equal(schemaNames, recordNames);
    }

    [Theory]
    [MemberData(nameof(MissingPropertyFixtures))]
    public void ShouldRejectEnvelopeWithoutRequiredProperty(string fileName)
    {
        string json = File.ReadAllText(Path.Combine(invalidFolder, fileName));

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, EnvelopeJsonContext.Default.Envelope));
    }

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ShouldAcceptGoldenEnvelope(string fileName)
    {
        byte[] json = File.ReadAllBytes(Path.Combine(goldenFolder, fileName));

        bool accepted = EnvelopeRules.TryParse(json, out Envelope? envelope);

        Assert.True(accepted);
        Assert.NotNull(envelope);
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void ShouldRejectInvalidEnvelope(string fileName)
    {
        byte[] json = File.ReadAllBytes(Path.Combine(invalidFolder, fileName));

        Assert.False(EnvelopeRules.TryParse(json, out _));
    }

    [Fact]
    public void ShouldFindEveryInvalidFixture()
    {
        Assert.Equal(35, InvalidFixtures.Count);
    }

    [Theory]
    [InlineData("\"kind\":\"presence\",")]
    [InlineData("\"extra\":\"\\ud800\",")]
    [InlineData("\"\\ud800\":1,")]
    [InlineData("\"extra\":1e400,")]
    public void ShouldRejectEnvelopeWithUnsafeJson(string extra)
    {
        byte[] json = WithExtra(extra);

        Assert.False(EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData(ContractLimits.MaxJsonDepth - 1, true)]
    [InlineData(ContractLimits.MaxJsonDepth, false)]
    public void ShouldLimitNestingDepth(int arrays, bool accepted)
    {
        byte[] json = WithExtra($"\"extra\":{new string('[', arrays)}{new string(']', arrays)},");

        Assert.Equal(accepted, EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData("-", ContractLimits.MaxIntegerDigits, true)]
    [InlineData("-", ContractLimits.MaxIntegerDigits + 1, false)]
    [InlineData("", ContractLimits.MaxIntegerDigits + 1, false)]
    [InlineData("0.", ContractLimits.MaxIntegerDigits + 1, true)]
    public void ShouldLimitIntegerDigits(string prefix, int digits, bool accepted)
    {
        byte[] json = WithExtra($"\"extra\":{prefix}{new string('9', digits)},");

        Assert.Equal(accepted, EnvelopeRules.TryParse(json, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(maxSeq)]
    public void ShouldAcceptDataSeqAtBounds(long seq)
    {
        string json = File.ReadAllText(Path.Combine(goldenFolder, "data.json"));
        string bounded = json.Replace("\"seq\": 1,", $"\"seq\": {seq},", StringComparison.Ordinal);

        Assert.True(EnvelopeRules.TryParse(Encoding.UTF8.GetBytes(bounded), out _));
    }

    private static byte[] WithExtra(string extra)
    {
        string json = File.ReadAllText(Path.Combine(goldenFolder, "data.json"));
        return Encoding.UTF8.GetBytes(json.Replace("\"v\": 1,", "\"v\": 1, " + extra, StringComparison.Ordinal));
    }

    private static TheoryData<string> FileNames(string folder, string pattern)
    {
        IEnumerable<string> names = Directory.GetFiles(folder, pattern)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order();
        return new TheoryData<string>(names);
    }
}
