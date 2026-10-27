using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Jobranger.Contracts.Ads;
using Jobranger.Contracts.Applications;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Replies;
using Jobranger.Contracts.Runs;
using Jobranger.Contracts.Settings;
using Jobranger.Contracts.Settings.Patches;

namespace Jobranger.Contracts.Tests.Payloads;

public sealed class PayloadCodecTests {
    private const int goldenCount = 55;
    private const int invalidCount = 160;
    private const int oversizeItems = 40000;
    private const string adMark = "ad.mark.json";
    private const string adsSnapshot = "ads.snapshot.json";
    private const string applicationCreate = "application.create.json";
    private const string arrayStart = "[";
    private const string objectStart = "{\"a\":";
    private const int maxTitleChars = 200;
    private const int maxPlaces = 20;
    private const string messageId = "00000000-0000-4000-8000-000000000003";
    private const byte quote = (byte)'"';
    private const byte backslash = (byte)'\\';

    public static TheoryData<string> GoldenFixtures => PayloadFixtures.GoldenNames;

    public static TheoryData<string> InvalidFixtures => PayloadFixtures.InvalidNames;

    private static ReadOnlySpan<byte> JsonWhiteSpace => " \n\r\t"u8;

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ShouldWriteFixtureBytes_WhenFixtureIsGolden(string fileName) {
        byte[] golden = PayloadFixtures.Golden(fileName);
        IPayload decoded = PayloadCodec.DecodePayload(golden);

        byte[] encoded = PayloadCodec.Encode(decoded);

        Assert.Equal(Encoding.UTF8.GetString(Minified(golden)), Encoding.UTF8.GetString(encoded));
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void ShouldRejectPayload_WhenFixtureIsInvalid(string fileName) {
        byte[] invalid = PayloadFixtures.Invalid(fileName);

        Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(invalid));
    }

    [Fact]
    public void ShouldFindEveryPayloadFixture_WhenFixturesAreLinked() {
        Assert.Equal((goldenCount, invalidCount), (GoldenFixtures.Count, InvalidFixtures.Count));
    }

    [Fact]
    public void ShouldRejectPayload_WhenKeyRepeats() {
        byte[] repeated = PayloadFixtures.GoldenWithExtra(adMark, "\"mark\": \"bogus\",");

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(repeated));

        Assert.Equal("payload $ fails duplicate_key", error.Message);
    }

    [Fact]
    public void ShouldRejectPayload_WhenNumberIsNotFinite() {
        byte[] json = PayloadFixtures.GoldenWithExtra(adMark, "\"extra\": 1e400,");

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("non_finite", error.Keyword);
    }

    [Fact]
    public void ShouldRejectPayload_WhenJsonIsMalformed() {
        byte[] json = PayloadFixtures.GoldenWithExtra(adMark, "\"extra\": NaN,");

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("json", error.Keyword);
    }

    [Theory]
    [InlineData("\"extra\": \"\\ud800\",")]
    [InlineData("\"\\udc00\": 1,")]
    public void ShouldRejectPayload_WhenTextHasLoneSurrogate(string extra) {
        byte[] json = PayloadFixtures.GoldenWithExtra(adMark, extra);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("surrogate", error.Keyword);
    }

    [Theory]
    [InlineData(arrayStart, ']', ContractLimits.MaxJsonDepth - 1, null)]
    [InlineData(arrayStart, ']', ContractLimits.MaxJsonDepth, "depth")]
    [InlineData(objectStart, '}', ContractLimits.MaxJsonDepth - 1, null)]
    [InlineData(objectStart, '}', ContractLimits.MaxJsonDepth, "depth")]
    public void ShouldLimitNesting_WhenUnknownFieldIsDeep(string start, char end, int depth, string? keyword) {
        string nested = $"{string.Concat(Enumerable.Repeat(start, depth))}1{new string(end, depth)}";
        byte[] json = PayloadFixtures.GoldenWithExtra(adMark, $"\"extra\": {nested},");

        string? rejection = Rejection(json);

        Assert.Equal(keyword, rejection);
    }

    [Theory]
    [InlineData("-", ContractLimits.MaxIntegerDigits, null)]
    [InlineData("", ContractLimits.MaxIntegerDigits + 1, "integer_digits")]
    [InlineData("0.", ContractLimits.MaxIntegerDigits + 1, null)]
    public void ShouldLimitIntegerDigits_WhenUnknownFieldIsLong(string prefix, int digits, string? keyword) {
        byte[] json = PayloadFixtures.GoldenWithExtra(adMark, $"\"extra\": {prefix}{new string('9', digits)},");

        string? rejection = Rejection(json);

        Assert.Equal(keyword, rejection);
    }

    [Fact]
    public void ShouldRejectPayload_WhenBytesAreNotUtf8() {
        byte[] json = [.. PayloadFixtures.GoldenWithExtra(adMark, "\"extra\": \"x\",")];
        int marker = Array.IndexOf(json, (byte)'x');
        json[marker] = 0xC0;

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("utf8", error.Keyword);
    }

    [Theory]
    [InlineData("{\"type\":\"bogus\",\"v\":1}", "bogus")]
    [InlineData("{\"type\":5,\"v\":1}", null)]
    [InlineData("[]", null)]
    public void ShouldRejectPayload_WhenTypeIsUnknown(string json, string? messageType) {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(utf8));

        Assert.Equal(("payload $.type fails const", messageType), (error.Message, error.MessageType));
    }

    [Theory]
    [InlineData("ad.mark.bad-mark.json", "payload $.mark fails enum", "ad.mark")]
    [InlineData("ads.snapshot.hidden-counts-sum-mismatch.json", "payload $.hidden_counts fails hidden_total", "ads.snapshot")]
    [InlineData("ads.snapshot.hidden-counts-extra-string.json", "payload $.hidden_counts fails hidden_total", "ads.snapshot")]
    [InlineData("ads.snapshot.hidden-kind-under-count.json", "payload $.hidden_counts.senior fails below_listed", "ads.snapshot")]
    [InlineData("application.update.float-integer.json", "payload $.application_id fails type", "application.update")]
    [InlineData("ads.snapshot.javascript-url.json", "payload $.active[0].url fails pattern", "ads.snapshot")]
    [InlineData("config.update.duplicate-time.json", "payload $.patch.schedule.times fails uniqueItems", "config.update")]
    [InlineData("models.snapshot.missing-usd-per-day.json", "payload $.providers[0].models[0] fails required", "models.snapshot")]
    public void ShouldNameRuleWithoutInput_WhenFixtureIsInvalid(string fileName, string message, string messageType) {
        byte[] invalid = PayloadFixtures.Invalid(fileName);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(invalid));

        Assert.Equal((message, messageType), (error.Message, error.MessageType));
    }

    [Fact]
    public void ShouldNameMaxItems_WhenArrayIsOversize() {
        JsonObject update = PayloadFixtures.GoldenNode("config.update.full.json");
        update["patch"]!["targets"]!["subfields"] = new JsonArray([.. Enumerable.Range(0, oversizeItems).Select(index => new JsonObject { ["a"] = index })]);
        byte[] json = PayloadFixtures.Utf8(update);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.patch.targets.subfields fails maxItems", error.Message);
    }

    [Fact]
    public void ShouldRejectPayload_WhenDayIsNotCalendarDate() {
        JsonObject snapshot = PayloadFixtures.GoldenNode(adsSnapshot);
        snapshot["as_of"] = "2026-02-30";
        byte[] json = PayloadFixtures.Utf8(snapshot);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.as_of fails format", error.Message);
    }

    [Fact]
    public void ShouldNameType_WhenHiddenTotalIsWholeFloat() {
        JsonObject snapshot = PayloadFixtures.GoldenNode(adsSnapshot);
        snapshot["hidden_total"] = JsonNode.Parse("2.0");
        byte[] json = PayloadFixtures.Utf8(snapshot);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.hidden_total fails type", error.Message);
    }

    [Fact]
    public void ShouldNameType_WhenHiddenCountIsWholeFloat() {
        JsonObject snapshot = PayloadFixtures.GoldenNode(adsSnapshot);
        snapshot["hidden_counts"]!["years"] = JsonNode.Parse("1.0");
        byte[] json = PayloadFixtures.Utf8(snapshot);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.hidden_counts.years fails type", error.Message);
    }

    [Fact]
    public void ShouldNameMaxLength_WhenNullableUrlIsOversize() {
        JsonObject snapshot = PayloadFixtures.GoldenNode("applications.snapshot.json");
        snapshot["applications"]![1]!["url"] = "https://jobs.example.test/" + new string('a', ContractLimits.UrlMaxChars);
        byte[] json = PayloadFixtures.Utf8(snapshot);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.applications[1].url fails maxLength", error.Message);
    }

    [Fact]
    public void ShouldNameAnyOf_WhenNullableUrlHasWrongType() {
        JsonObject snapshot = PayloadFixtures.GoldenNode("applications.snapshot.json");
        snapshot["applications"]![1]!["url"] = 5;
        byte[] json = PayloadFixtures.Utf8(snapshot);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.DecodePayload(json));

        Assert.Equal("payload $.applications[1].url fails anyOf", error.Message);
    }

    [Fact]
    public void ShouldAcceptTitle_WhenAstralCharactersFillMaxLength() {
        JsonObject application = PayloadFixtures.GoldenNode(applicationCreate);
        application["title"] = string.Concat(Enumerable.Repeat("\U0001F600", maxTitleChars));
        byte[] json = PayloadFixtures.Utf8(application);

        ApplicationCreate decoded = PayloadCodec.Decode<ApplicationCreate>(json);

        Assert.Equal(maxTitleChars * 2, decoded.Title.Length);
    }

    [Fact]
    public void ShouldAcceptPlaces_WhenCountIsAtMax() {
        JsonObject update = PayloadFixtures.GoldenNode("config.update.full.json");
        update["patch"]!["targets"]!["places"] = new JsonArray([.. Enumerable.Range(0, maxPlaces).Select(index => JsonValue.Create($"Place {index}"))]);
        byte[] json = PayloadFixtures.Utf8(update);

        ConfigUpdate decoded = PayloadCodec.Decode<ConfigUpdate>(json);

        Assert.Equal(maxPlaces, decoded.Patch.Targets!.Places!.Count);
    }

    [Fact]
    public void ShouldRejectRecord_WhenTypeIsNotContractRecord() {
        ForeignPayload foreign = new();

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Encode(foreign));

        Assert.Equal("payload $.type fails const", error.Message);
    }

    [Fact]
    public void ShouldRejectPayload_WhenDecodedTypeIsNotRequested() {
        byte[] golden = PayloadFixtures.Golden(adMark);

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Decode<AdUnmark>(golden));

        Assert.Equal(("payload $.type fails const", "ad.mark"), (error.Message, error.MessageType));
    }

    [Fact]
    public void ShouldWriteCompactUtf8_WhenEncodingPayload() {
        AdMark mark = new(messageId, "bø😀\"\n", "not_for_me");

        byte[] encoded = PayloadCodec.Encode(mark);

        Assert.Equal(
            "{\"type\":\"ad.mark\",\"v\":1,\"id\":\"00000000-0000-4000-8000-000000000003\",\"ad_id\":\"bø😀\\\"\\n\",\"mark\":\"not_for_me\"}",
            Encoding.UTF8.GetString(encoded));
    }

    [Theory]
    [InlineData("\r", "\\r")]
    [InlineData("\t", "\\t")]
    [InlineData("\b", "\\b")]
    [InlineData("\f", "\\f")]
    [InlineData("\\", "\\\\")]
    [InlineData("\u0001", "\\u0001")]
    [InlineData("\t ", "\\t ")]
    public void ShouldEscapeCharacter_WhenJsonRequiresIt(string text, string escaped) {
        AdMark mark = new(messageId, $"a{text}b", "not_for_me");

        byte[] encoded = PayloadCodec.Encode(mark);

        string json = Encoding.UTF8.GetString(encoded);
        Assert.Contains($"\"ad_id\":\"a{escaped}b\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRejectRecord_WhenEncodedPayloadBreaksSchema() {
        byte[] golden = PayloadFixtures.Golden("config.snapshot.json");
        ConfigSnapshot decoded = PayloadCodec.Decode<ConfigSnapshot>(golden);
        ConfigSnapshot snapshot = decoded with { Schedule = decoded.Schedule with { Times = ["03:00", "03:00"] } };

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Encode(snapshot));

        Assert.Equal("uniqueItems", error.Keyword);
    }

    [Fact]
    public void ShouldOmitRecheck_WhenSavedHasNone() {
        Saved saved = new(messageId, "now");

        byte[] encoded = PayloadCodec.Encode(saved);

        JsonObject parsed = JsonNode.Parse(encoded)!.AsObject();
        Assert.False(parsed.ContainsKey("recheck"));
    }

    [Fact]
    public void ShouldOmitAddress_WhenPatchLeavesItUnset() {
        ConfigUpdate update = new(messageId, new ConfigPatch(Notify: new NotifyPatch(Email: new EmailNotifyPatch(On: true))));

        byte[] encoded = PayloadCodec.Encode(update);

        JsonNode parsed = JsonNode.Parse(encoded)!;
        JsonObject email = parsed["patch"]!["notify"]!["email"]!.AsObject();
        Assert.False(email.ContainsKey("address"));
    }

    [Fact]
    public void ShouldRejectRecord_WhenNumberIsNotFinite() {
        byte[] golden = PayloadFixtures.Golden("run.finished.json");
        RunFinished decoded = PayloadCodec.Decode<RunFinished>(golden);
        RunFinished finished = decoded with { CostUsd = double.NaN };

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Encode(finished));

        Assert.Equal("payload $ fails serialize", error.Message);
    }

    [Theory]
    [InlineData("a", 0xD800, "b")]
    [InlineData("", 0xDC00, "b")]
    [InlineData("a", 0xD800, "")]
    public void ShouldRejectRecord_WhenTextHasLoneSurrogate(string before, int surrogate, string after) {
        byte[] golden = PayloadFixtures.Golden(applicationCreate);
        ApplicationCreate decoded = PayloadCodec.Decode<ApplicationCreate>(golden);
        ApplicationCreate application = decoded with { Title = $"{before}{(char)surrogate}{after}" };

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Encode(application));

        Assert.Equal("payload $ fails serialize", error.Message);
    }

    [Fact]
    public void ShouldRejectRecord_WhenRequiredTextIsNull() {
        byte[] golden = PayloadFixtures.Golden(applicationCreate);
        ApplicationCreate decoded = PayloadCodec.Decode<ApplicationCreate>(golden);
        ApplicationCreate application = decoded with { Company = null! };

        ContractException error = Assert.Throws<ContractException>(() => PayloadCodec.Encode(application));

        Assert.Equal("payload $ fails serialize", error.Message);
    }

    [Fact]
    public void ShouldCoverEverySchemaProperty_WhenPayloadRecordsAreChecked() {
        JsonObject schema = PayloadFixtures.Schema;
        JsonObject definitions = schema["$defs"]!.AsObject();
        List<string> mismatches = [];

        foreach (JsonNode? reference in schema["oneOf"]!.AsArray()) {
            JsonObject definition = Resolve(reference!.AsObject(), definitions);
            string tag = definition["properties"]!["type"]!["const"]!.GetValue<string>();
            if (PayloadTypes.ByTag.TryGetValue(tag, out JsonTypeInfo? info)) {
                Compare(definition, info, tag, definitions, mismatches);
            } else {
                mismatches.Add(tag);
            }
        }

        Assert.Empty(mismatches);
    }

    private static byte[] Minified(byte[] json) {
        List<byte> compact = new(json.Length);
        bool inString = false;
        bool escaped = false;
        foreach (byte current in json) {
            if (inString) {
                compact.Add(current);
                if (escaped) {
                    escaped = false;
                } else if (current == backslash) {
                    escaped = true;
                } else if (current == quote) {
                    inString = false;
                }
            } else if (!JsonWhiteSpace.Contains(current)) {
                compact.Add(current);
                inString = current == quote;
            }
        }
        return [.. compact];
    }

    private static string? Rejection(byte[] json) {
        try {
            _ = PayloadCodec.DecodePayload(json);
            return null;
        } catch (ContractException error) {
            return error.Keyword;
        }
    }

    private static void Compare(JsonObject definition, JsonTypeInfo info, string where, JsonObject definitions, List<string> mismatches) {
        JsonObject properties = definition["properties"]!.AsObject();
        Dictionary<string, JsonPropertyInfo> record = info.Properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
        mismatches.AddRange(properties.Select(property => property.Key).Except(record.Keys).Select(name => $"{where}.{name} has no record property"));
        mismatches.AddRange(record.Keys.Except(properties.Select(property => property.Key)).Select(name => $"{where}.{name} is not in the schema"));
        foreach ((string name, JsonNode? node) in properties) {
            JsonObject? child = ObjectDefinition(node!.AsObject(), definitions);
            if (child is null || !record.TryGetValue(name, out JsonPropertyInfo? property)) {
                continue;
            }
            JsonTypeInfo childInfo = ElementInfo(property.PropertyType);
            if (childInfo.Kind == JsonTypeInfoKind.Object) {
                Compare(child, childInfo, $"{where}.{name}", definitions, mismatches);
            } else {
                mismatches.Add($"{where}.{name} is not an object record");
            }
        }
    }

    private static JsonTypeInfo ElementInfo(Type type) {
        JsonTypeInfo info = ContractsJsonContext.Default.GetTypeInfo(Nullable.GetUnderlyingType(type) ?? type)!;
        return info.Kind == JsonTypeInfoKind.Enumerable ? ContractsJsonContext.Default.GetTypeInfo(info.ElementType!)! : info;
    }

    private static JsonObject? ObjectDefinition(JsonObject node, JsonObject definitions) {
        JsonObject resolved = Resolve(node, definitions);
        if (resolved.ContainsKey("properties")) {
            return resolved;
        }
        if (resolved["items"] is JsonObject items) {
            return ObjectDefinition(items, definitions);
        }
        return resolved["anyOf"]?.AsArray()
            .Select(branch => ObjectDefinition(branch!.AsObject(), definitions))
            .FirstOrDefault(branch => branch is not null);
    }

    private static JsonObject Resolve(JsonObject node, JsonObject definitions) =>
        node["$ref"] is JsonValue reference
            ? Resolve(definitions[reference.GetValue<string>().Split('/')[^1]]!.AsObject(), definitions)
            : node;

    private sealed class ForeignPayload : IPayload {
        public string Type => AdMark.Tag;

        public int V => ContractLimits.ContractVersion;
    }
}
