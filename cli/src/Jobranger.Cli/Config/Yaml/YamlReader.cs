using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Jobranger.Cli.Config.Yaml;

internal sealed partial class YamlReader(string text, string fileName) {
    private const char commentMark = '#';
    private const char newLine = '\n';
    private const char carriageReturn = '\r';
    private const char tab = '\t';
    private const char space = ' ';
    private const string inlineLeadIn = " \t:";
    private const int bracketWidth = 1;
    private const int maxDepth = 32;
    private const int firstLine = 1;

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
        } catch (Exception error) when (error is InvalidOperationException or ArgumentException) {
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
                return scalar.Style == ScalarStyle.Plain && YamlNull.Literals.Contains(scalar.Value)
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

    private long ErrorLine(YamlException error) {
        if (error is not SyntaxErrorException) {
            return error.Start.Line;
        }
        if (error.Start.Index == 0) {
            return TabIndentLine(parser.Current?.Start.Line ?? firstLine);
        }
        return IsTab(error.End) ? error.End.Line : error.Start.Line;
    }

    private bool IsTab(Mark mark) => mark.Index < text.Length && text[At(mark)] == tab;

    private long TabIndentLine(long fromLine) {
        long line = firstLine;
        int? scalarIndent = null;
        for (int lineStart = 0; lineStart < text.Length; lineStart = LineEnd(lineStart) + 1) {
            string content = text[lineStart..LineEnd(lineStart)];
            int indent = content.TakeWhile(character => character == space).Count();
            bool blank = content.TrimEnd(carriageReturn).Length == indent;
            bool inBlockScalar = scalarIndent is int open && (blank || indent > open);
            if (!inBlockScalar) {
                if (line >= fromLine && content.TakeWhile(char.IsWhiteSpace).Contains(tab)) {
                    return line;
                }
                scalarIndent = BlockScalarStart().IsMatch(content) ? indent : null;
            }
            line++;
        }
        return fromLine;
    }

    private static YamlTree Failed(ConfigError error) =>
        new(null, new Dictionary<string, KeyComments>(), new Dictionary<string, int>(), [], [error]);

    private ConfigError NotYaml(long line) =>
        new(fileName, ConfigProblem.NotYaml, line.ToString(CultureInfo.InvariantCulture));

    private static int At(Mark mark) => checked((int)mark.Index);

    private static string CommentText(string comment) {
        string body = comment[1..];
        return body.StartsWith(' ') ? body[1..] : body;
    }

    [GeneratedRegex(@"(^\s*-|:)\s+[|>][-+0-9]*\s*(#.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockScalarStart();
}
