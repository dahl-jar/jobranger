using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Jobranger.Cli.Config.Yaml;

internal static partial class YamlEmitter {
    private const int maxWidth = int.MaxValue;
    private const int indent = 2;
    private const char tab = '\t';
    private const long afterEveryKey = long.MaxValue;
    private const long ranksPerKey = 2;
    private const string boolPattern =
        "^(true|True|TRUE|false|False|FALSE|y|Y|n|N|yes|Yes|YES|no|No|NO|on|On|ON|off|Off|OFF)$";
    private const string numberPattern =
        @"^([-+]?[0-9]+|0o[0-7]+|0x[0-9a-fA-F]+|[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?"
        + @"|[-+]?\.(inf|Inf|INF)|\.(nan|NaN|NAN))$";
    private const string timestampPattern = "^[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}([Tt ].*)?$";

    private static readonly EmitterSettings emitterSettings =
        EmitterSettings.Default.WithBestIndent(indent).WithBestWidth(maxWidth);

    public static string Emit(JsonObject model, YamlTree? previous) {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        Emitter emitter = new(writer, emitterSettings);
        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, isImplicit: true));
        emitter.Emit(new MappingStart(null, null, isImplicit: true, MappingStyle.Block));
        (IReadOnlyList<string> header, YamlTree? body) = SplitHeader(previous);
        EmitComments(emitter, header);
        EmitEntries(emitter, model, string.Empty, body);
        EmitComments(emitter, previous?.Trailing ?? []);
        emitter.Emit(new MappingEnd());
        emitter.Emit(new DocumentEnd(isImplicit: true));
        emitter.Emit(new StreamEnd());
        return writer.ToString();
    }

    private static (IReadOnlyList<string> Header, YamlTree? Body) SplitHeader(YamlTree? previous) {
        string? firstKey = previous?.KeyOrder.Where(pair => pair.Value == 0).Select(pair => pair.Key).FirstOrDefault();
        if (previous is null || firstKey is null || !previous.Comments.TryGetValue(firstKey, out KeyComments? first)) {
            return ([], previous);
        }
        Dictionary<string, KeyComments> comments = new(previous.Comments) { [firstKey] = first with { Leading = [] } };
        return (first.Leading, previous with { Comments = comments });
    }

    private static void EmitEntries(IEmitter emitter, JsonObject mapping, string path, YamlTree? previous) {
        foreach (KeyValuePair<string, JsonNode?> entry in InFileOrder(mapping, path, previous)) {
            EmitEntry(emitter, entry, ConfigLocation.Join(path, entry.Key), previous);
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> InFileOrder(
        JsonObject mapping, string path, YamlTree? previous) {
        if (previous is null) {
            return mapping;
        }
        List<KeyValuePair<string, JsonNode?>> entries = [.. mapping];
        long[] ranks = new long[entries.Count];
        long nextKnownRank = afterEveryKey;
        for (int index = entries.Count - 1; index >= 0; index--) {
            if (previous.KeyOrder.TryGetValue(ConfigLocation.Join(path, entries[index].Key), out int position)) {
                nextKnownRank = (ranksPerKey * position) + 1;
                ranks[index] = nextKnownRank;
            } else {
                ranks[index] = nextKnownRank - 1;
            }
        }
        return entries.Zip(ranks).OrderBy(pair => pair.Second).Select(pair => pair.First);
    }

    private static void EmitEntry(
        IEmitter emitter, KeyValuePair<string, JsonNode?> entry, string location, YamlTree? previous) {
        KeyComments? comments = previous?.Comments.GetValueOrDefault(location);
        EmitComments(emitter, comments?.Leading ?? []);
        emitter.Emit(PlainScalar(entry.Key));
        bool block = IsBlock(entry.Value);
        if (block) {
            EmitInline(emitter, comments);
        }
        EmitValue(emitter, entry.Value, location, previous);
        if (!block) {
            EmitInline(emitter, comments);
        }
    }

    private static void EmitComments(IEmitter emitter, IEnumerable<string> comments) {
        foreach (string comment in comments) {
            emitter.Emit(new Comment(comment, isInline: false));
        }
    }

    private static void EmitInline(IEmitter emitter, KeyComments? comments) {
        if (comments?.Inline is string inline) {
            emitter.Emit(new Comment(inline, isInline: true));
        }
    }

    private static void EmitValue(IEmitter emitter, JsonNode? node, string path, YamlTree? previous) {
        switch (node) {
            case JsonObject mapping:
                emitter.Emit(new MappingStart(null, null, isImplicit: true, MappingStyle.Block));
                EmitEntries(emitter, mapping, path, previous);
                emitter.Emit(new MappingEnd());
                break;
            case JsonArray list:
                SequenceStyle style = IsBlock(list) ? SequenceStyle.Block : SequenceStyle.Flow;
                emitter.Emit(new SequenceStart(null, null, isImplicit: true, style));
                foreach (JsonNode? item in list) {
                    EmitValue(emitter, item, string.Empty, previous: null);
                }
                emitter.Emit(new SequenceEnd());
                break;
            case JsonValue value:
                emitter.Emit(ScalarFor(value));
                break;
            default:
                emitter.Emit(PlainScalar(YamlNull.Text));
                break;
        }
    }

    private static Scalar ScalarFor(JsonValue value) => value.GetValueKind() switch {
        JsonValueKind.True => PlainScalar(BoolText.True),
        JsonValueKind.False => PlainScalar(BoolText.False),
        JsonValueKind.Number => PlainScalar(value.ToJsonString()),
        _ => TextScalar(value.GetValue<string>()),
    };

    private static Scalar TextScalar(string text) =>
        NeedsQuotes(text)
            ? new Scalar(null, null, text, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true)
            : PlainScalar(text);

    private static Scalar PlainScalar(string text) =>
        new(null, null, text, ScalarStyle.Plain, isPlainImplicit: true, isQuotedImplicit: false);

    private static bool IsBlock(JsonNode? node) =>
        node is JsonObject || node is JsonArray list && list.Any(item => item is JsonObject or JsonArray);

    private static bool NeedsQuotes(string text) =>
        YamlNull.Literals.Contains(text) || CoreScalar().IsMatch(text)
            || text.Any(character => character == tab || YamlCharacters.IsLineSeparator(character));

    [GeneratedRegex(boolPattern + "|" + numberPattern + "|" + timestampPattern, RegexOptions.CultureInvariant)]
    private static partial Regex CoreScalar();
}
