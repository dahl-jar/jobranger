using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Jobranger.Cli.Config;

internal static partial class ConfigFile {
    private const char commentMark = '#';
    private const char newLine = '\n';
    private const char tab = '\t';
    private const string nullText = "null";
    private const string inlineLeadIn = " \t:";
    private const int maxWidth = int.MaxValue;
    private const int indent = 2;
    private const int bracketWidth = 1;
    private const int maxDepth = 32;
    private const int firstLine = 1;
    private const long afterEveryKey = long.MaxValue;
    private const long ranksPerKey = 2;
    private const string boolPattern =
        "^(true|True|TRUE|false|False|FALSE|y|Y|n|N|yes|Yes|YES|no|No|NO|on|On|ON|off|Off|OFF)$";
    private const string numberPattern =
        @"^([-+]?[0-9]+|0o[0-7]+|0x[0-9a-fA-F]+|[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?"
        + @"|[-+]?\.(inf|Inf|INF)|\.(nan|NaN|NAN))$";
    private const string timestampPattern = "^[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}([Tt ].*)?$";

    private static readonly FrozenSet<string> nullLiterals =
        FrozenSet.Create(StringComparer.Ordinal, string.Empty, "~", nullText, "Null", "NULL");

    private static readonly EmitterSettings emitterSettings =
        EmitterSettings.Default.WithBestIndent(indent).WithBestWidth(maxWidth);

    private static YamlTree ParseYaml(string text, string fileName) {
        YamlReader reader = new(text, fileName);
        return reader.Read();
    }

    private static string EmitYaml(JsonObject model, YamlTree? previous) {
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
                emitter.Emit(PlainScalar(nullText));
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
        ResolvesToOtherType(text)
            ? new Scalar(null, null, text, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true)
            : PlainScalar(text);

    private static Scalar PlainScalar(string text) =>
        new(null, null, text, ScalarStyle.Plain, isPlainImplicit: true, isQuotedImplicit: false);

    private static bool IsBlock(JsonNode? node) =>
        node is JsonObject || node is JsonArray list && list.Any(item => item is JsonObject or JsonArray);

    private static bool ResolvesToOtherType(string text) => nullLiterals.Contains(text) || CoreScalar().IsMatch(text);

    [GeneratedRegex(boolPattern + "|" + numberPattern + "|" + timestampPattern, RegexOptions.CultureInvariant)]
    private static partial Regex CoreScalar();

    private sealed class YamlReader(string text, string fileName) {
        private readonly Parser parser = new(new Scanner(new StringReader(text), skipComments: true));
        private readonly List<(int Start, int End)> scalarSpans = [];
        private readonly List<(string Path, bool KeepsComments, int Start)> keys = [];
        private readonly Dictionary<string, string> inlineComments = [];
        private readonly List<ConfigError> errors = [];
        private int lastEnd;
        private ConfigError? alias;

        public YamlTree Read() {
            JsonNode? root = null;
            try {
                parser.Consume<StreamStart>();
                if (parser.TryConsume<DocumentStart>(out _)) {
                    root = ReadNode(string.Empty, keepsComments: true, depth: 0);
                    parser.Consume<DocumentEnd>();
                }
                parser.Consume<StreamEnd>();
            } catch (YamlException error) {
                return Failed(alias ?? NotYaml(ErrorLine(error)));
            } catch (InvalidOperationException) {
                return Failed(NotYaml(parser.Current?.End.Line ?? firstLine));
            }
            return AttachComments(root);
        }

        private JsonNode? ReadNode(string path, bool keepsComments, int depth) {
            ParsingEvent current = parser.Current!;
            if (depth > maxDepth) {
                throw new YamlException(
                    current.Start,
                    current.End,
                    string.Create(CultureInfo.InvariantCulture, $"nesting deeper than {maxDepth} levels"));
            }
            switch (current) {
                case AnchorAlias:
                    alias = new ConfigError(path, ConfigProblem.AliasUsed);
                    throw new YamlException(current.Start, current.End, "alias in config");
                case Scalar scalar:
                    parser.MoveNext();
                    lastEnd = At(scalar.End);
                    scalarSpans.Add((At(scalar.Start), lastEnd));
                    return scalar.Style == ScalarStyle.Plain && nullLiterals.Contains(scalar.Value)
                        ? null
                        : JsonValue.Create(scalar.Value);
                case SequenceStart sequence:
                    return ReadList(sequence, path, depth);
                case MappingStart mapping:
                    return ReadMapping(mapping, path, keepsComments, depth);
                default:
                    throw new YamlException(current.Start, current.End, "unexpected YAML event");
            }
        }

        private JsonArray ReadList(SequenceStart sequence, string path, int depth) {
            parser.MoveNext();
            JsonArray list = [];
            while (parser.Current is not SequenceEnd) {
                list.Add(ReadNode(ConfigLocation.Item(path, list.Count), keepsComments: false, depth + 1));
            }
            SequenceEnd end = parser.Consume<SequenceEnd>();
            if (sequence.Style == SequenceStyle.Flow) {
                lastEnd = At(end.End) + bracketWidth;
            }
            return list;
        }

        private JsonObject ReadMapping(MappingStart mapping, string path, bool keepsComments, int depth) {
            parser.MoveNext();
            JsonObject entries = [];
            while (parser.Current is not MappingEnd) {
                if (parser.Current is not Scalar key) {
                    throw new YamlException(parser.Current!.Start, parser.Current.End, "mapping key is not text");
                }
                parser.MoveNext();
                string location = ConfigLocation.Join(path, key.Value);
                keys.Add((location, keepsComments, At(key.Start)));
                bool blockValue = parser.Current is SequenceStart { Style: SequenceStyle.Block }
                    or MappingStart { Style: MappingStyle.Block };
                JsonNode? value = ReadNode(
                    location, keepsComments && mapping.Style == MappingStyle.Block, depth + 1);
                if (entries.ContainsKey(key.Value)) {
                    errors.Add(new ConfigError(location, ConfigProblem.DuplicateKey));
                    continue;
                }
                entries[key.Value] = value;
                if (keepsComments && ReadInlineComment(blockValue ? At(key.End) : lastEnd) is string inline) {
                    inlineComments[location] = inline;
                }
            }
            MappingEnd end = parser.Consume<MappingEnd>();
            if (mapping.Style == MappingStyle.Flow) {
                lastEnd = At(end.End) + bracketWidth;
            }
            return entries;
        }

        private string? ReadInlineComment(int from) {
            if (text[from - 1] == newLine) {
                return null;
            }
            string rest = text[from..LineEnd(from)]
                .TrimStart(inlineLeadIn.ToCharArray())
                .TrimEnd();
            return rest.Length > 0 && rest[0] == commentMark ? CommentText(rest) : null;
        }

        private YamlTree AttachComments(JsonNode? root) {
            Dictionary<string, List<string>> leading = [];
            List<string> trailing = [];
            int keyIndex = 0;
            int lineStart = 0;
            while (lineStart < text.Length) {
                int lineEnd = LineEnd(lineStart);
                string line = text[lineStart..lineEnd].Trim();
                if (line.Length > 0 && line[0] == commentMark && !IsInsideScalar(lineStart)) {
                    while (keyIndex < keys.Count && keys[keyIndex].Start < lineStart) {
                        keyIndex++;
                    }
                    if (keyIndex == keys.Count) {
                        trailing.Add(CommentText(line));
                    } else if (keys[keyIndex].KeepsComments) {
                        string path = keys[keyIndex].Path;
                        leading.TryAdd(path, []);
                        leading[path].Add(CommentText(line));
                    }
                }
                lineStart = lineEnd + 1;
            }
            Dictionary<string, KeyComments> comments = [];
            foreach (string path in leading.Keys.Union(inlineComments.Keys)) {
                comments[path] = new KeyComments(
                    leading.GetValueOrDefault(path) ?? [], inlineComments.GetValueOrDefault(path));
            }
            return new YamlTree(root, comments, KeyOrder(), trailing, errors);
        }

        private Dictionary<string, int> KeyOrder() {
            Dictionary<string, int> order = [];
            foreach ((string path, _, _) in keys) {
                order.TryAdd(path, order.Count);
            }
            return order;
        }

        private int LineEnd(int from) {
            int end = text.IndexOf(newLine, from);
            return end < 0 ? text.Length : end;
        }

        private bool IsInsideScalar(int index) => scalarSpans.Any(span => index > span.Start && index < span.End);

        private long ErrorLine(YamlException error) =>
            error is SyntaxErrorException && error.Start.Index == 0
                ? TabIndentLine(parser.Current?.Start.Line ?? firstLine)
                : error.Start.Line;

        private long TabIndentLine(long fromLine) {
            long line = firstLine;
            for (int lineStart = 0; lineStart < text.Length; lineStart = LineEnd(lineStart) + 1) {
                if (line >= fromLine && IsTabIndented(lineStart)) {
                    return line;
                }
                line++;
            }
            return fromLine;
        }

        private bool IsTabIndented(int lineStart) =>
            text[lineStart..LineEnd(lineStart)].TakeWhile(char.IsWhiteSpace).Contains(tab);

        private static YamlTree Failed(ConfigError error) =>
            new(null, new Dictionary<string, KeyComments>(), new Dictionary<string, int>(), [], [error]);

        private ConfigError NotYaml(long line) =>
            new(fileName, ConfigProblem.NotYaml, line.ToString(CultureInfo.InvariantCulture));

        private static int At(Mark mark) => checked((int)mark.Index);

        private static string CommentText(string comment) {
            string body = comment[1..];
            return body.StartsWith(' ') ? body[1..] : body;
        }
    }
}
