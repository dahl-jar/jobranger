using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config.Yaml;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed class ConfigSection {
    private const string choiceSeparator = ", ";

    private readonly JsonObject values;
    private readonly string path;
    private readonly List<ConfigError> errors;
    private readonly IReadOnlyDictionary<string, YamlScalar> scalars;

    public ConfigSection(JsonObject values, string path, IReadOnlyList<string> known, List<ConfigError> errors)
        : this(values, path, known, errors, FrozenDictionary<string, YamlScalar>.Empty) {
    }

    public ConfigSection(
        JsonObject values,
        string path,
        IReadOnlyList<string> known,
        List<ConfigError> errors,
        IReadOnlyDictionary<string, YamlScalar> scalars) {
        this.values = values;
        this.path = path;
        this.errors = errors;
        this.scalars = scalars;
        foreach (string key in values.Select(entry => entry.Key).Where(key => !known.Contains(key))) {
            Fail(key, ConfigProblem.UnknownKey);
        }
    }

    public ConfigSection? Child(string key, IReadOnlyList<string> known, bool required = false) {
        if (IsMissing(key)) {
            if (required) {
                Fail(key, ConfigProblem.Required);
            }
            return null;
        }
        return Of(values[key], Location(key), known, errors, scalars);
    }

    public List<ConfigSection>? Sections(string key, IReadOnlyList<string> known) {
        if (List(key) is not JsonArray items) {
            return null;
        }
        List<ConfigSection> sections = [];
        for (int index = 0; index < items.Count; index++) {
            if (Of(items[index], ItemLocation(key, index), known, errors, scalars) is ConfigSection section) {
                sections.Add(section);
            }
        }
        return sections;
    }

    public string? Text(string key) {
        if (!values.TryGetPropertyValue(key, out JsonNode? node) || node is null) {
            return null;
        }
        if (node is JsonValue value && value.TryGetValue(out string? text)) {
            return text;
        }
        Fail(key, ConfigProblem.NotText);
        return null;
    }

    public static void CheckLength(string text, string location, int limit, List<ConfigError> errors) {
        if (CodePoints.Count(text) > limit) {
            errors.Add(new ConfigError(location, ConfigProblem.TooLong, limit.ToString(CultureInfo.InvariantCulture)));
        }
    }

    public string? RequiredText(string key, bool blankAllowed = false) {
        if (IsMissing(key) || (!blankAllowed && IsBlank(key))) {
            Fail(key, ConfigProblem.Required);
            return null;
        }
        return Text(key);
    }

    public bool? Bool(string key) {
        string? text = Text(key);
        if (text is null) {
            return null;
        }
        if (string.Equals(text, BoolText.True, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (string.Equals(text, BoolText.False, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        Fail(key, ConfigProblem.NotBoolean);
        return null;
    }

    public decimal? Number(string key) {
        string? text = Text(key);
        if (text is null) {
            return null;
        }
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) {
            return number;
        }
        Fail(key, ConfigProblem.NotNumber);
        return null;
    }

    public decimal? RequiredNumber(string key) {
        if (IsMissing(key)) {
            Fail(key, ConfigProblem.Required);
            return null;
        }
        return Number(key);
    }

    public TEnum? Choice<TEnum>(string key) where TEnum : struct, Enum {
        TEnum[] choices = Enum.GetValues<TEnum>();
        string? text = ChoiceText(key, [.. choices.Select(WireNames.Of)]);
        return text is null ? null : choices.Single(choice => WireNames.Of(choice) == text);
    }

    public string? ChoiceText(string key, IReadOnlyList<string> allowed) {
        string? text = Text(key);
        if (text is null || allowed.Contains(text)) {
            return text;
        }
        Fail(key, ConfigProblem.NotChoice, string.Join(choiceSeparator, allowed));
        return null;
    }

    public JsonArray? List(string key) {
        if (!values.TryGetPropertyValue(key, out JsonNode? node)) {
            return null;
        }
        if (node is JsonArray items) {
            return items;
        }
        Fail(key, ConfigProblem.NotList);
        return null;
    }

    public List<string>? TextList(string key) {
        if (List(key) is not JsonArray items) {
            return null;
        }
        List<string> texts = [];
        for (int index = 0; index < items.Count; index++) {
            if (items[index] is JsonValue value && value.TryGetValue(out string? text)) {
                texts.Add(text);
            } else {
                errors.Add(new ConfigError(ItemLocation(key, index), ConfigProblem.NotText));
            }
        }
        return texts;
    }

    public List<string>? ChoiceList(string key, IReadOnlyList<string> allowed) {
        if (TextList(key) is not List<string> texts) {
            return null;
        }
        for (int index = 0; index < texts.Count; index++) {
            if (!allowed.Contains(texts[index])) {
                errors.Add(new ConfigError(
                    ItemLocation(key, index), ConfigProblem.NotChoice, string.Join(choiceSeparator, allowed)));
            }
        }
        return [.. texts.Where(allowed.Contains)];
    }

    public string? StrictText(string key) {
        if (!values.ContainsKey(key)) {
            Fail(key, ConfigProblem.Required);
            return null;
        }
        YamlValue value = ValueAt(Location(key));
        if (value.Kind == YamlKind.Text) {
            return value.Text;
        }
        Fail(key, ConfigProblem.NotText);
        return null;
    }

    public string? OptionalText(string key) {
        if (!values.ContainsKey(key)) {
            return null;
        }
        YamlValue value = ValueAt(Location(key));
        if (value.Kind == YamlKind.Text) {
            return value.Text;
        }
        if (value.Kind != YamlKind.Null) {
            Fail(key, ConfigProblem.NotText);
        }
        return null;
    }

    public bool? LaxFlag(string key) {
        if (!values.ContainsKey(key)) {
            return null;
        }
        bool? flag = Flag(ValueAt(Location(key)));
        if (flag is null) {
            Fail(key, ConfigProblem.NotBoolean);
        }
        return flag;
    }

    public double? LaxNumber(string key) {
        if (!values.ContainsKey(key)) {
            Fail(key, ConfigProblem.Required);
            return null;
        }
        double? number = Number(ValueAt(Location(key)));
        if (number is null) {
            Fail(key, ConfigProblem.NotNumber);
        }
        return number;
    }

    public List<string>? StrictTextList(string key) {
        if (List(key) is not JsonArray items) {
            return null;
        }
        List<string> texts = [];
        for (int index = 0; index < items.Count; index++) {
            YamlValue value = ValueAt(ItemLocation(key, index));
            if (value.Kind == YamlKind.Text) {
                texts.Add(value.Text);
            } else {
                errors.Add(new ConfigError(ItemLocation(key, index), ConfigProblem.NotText));
            }
        }
        return texts;
    }

    private static ConfigSection? Of(
        JsonNode? node,
        string location,
        IReadOnlyList<string> known,
        List<ConfigError> errors,
        IReadOnlyDictionary<string, YamlScalar> scalars) {
        if (node is JsonObject values) {
            return new ConfigSection(values, location, known, errors, scalars);
        }
        errors.Add(new ConfigError(location, ConfigProblem.NotSection));
        return null;
    }

    private YamlValue ValueAt(string location) =>
        scalars.TryGetValue(location, out YamlScalar? scalar) ? YamlTypes.Resolve(scalar) : new YamlValue(YamlKind.Other);

    private static bool? Flag(YamlValue value) => value.Kind switch {
        YamlKind.Bool => value.Number == 1,
        YamlKind.Int or YamlKind.Float when value.Number == 0 => false,
        YamlKind.Int or YamlKind.Float when value.Number == 1 => true,
        YamlKind.Text => PydanticLax.Bool(value.Text),
        _ => null,
    };

    private static double? Number(YamlValue value) => value.Kind switch {
        YamlKind.Int when double.IsFinite(value.Number) => value.Number,
        YamlKind.Float or YamlKind.Bool => value.Number,
        YamlKind.Text => PydanticLax.Float(value.Text),
        _ => null,
    };

    private string ItemLocation(string key, int index) => ConfigLocation.Item(Location(key), index);

    private bool IsMissing(string key) => !values.TryGetPropertyValue(key, out JsonNode? node) || node is null;

    private bool IsBlank(string key) =>
        values[key] is JsonValue value && value.TryGetValue(out string? text) && string.IsNullOrWhiteSpace(text);

    private string Location(string key) => ConfigLocation.Join(path, key);

    private void Fail(string key, ConfigProblem problem, string detail = "") =>
        errors.Add(new ConfigError(Location(key), problem, detail));
}
