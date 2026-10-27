using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Config.Yaml;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Profiles;

internal static class ContextFile {
    public const string AboutKey = "about";
    public const string DocsKey = "docs";
    public const string IdKey = "id";
    public const string NameKey = "name";
    public const string TextKey = "text";

    private const string addedAtKey = "added_at";

    private static readonly string[] contextKeys = [AboutKey, DocsKey];
    private static readonly string[] docKeys = [IdKey, NameKey, TextKey, addedAtKey];
    private static readonly UTF8Encoding strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static UserContext Load(string path) {
        YamlTree tree;
        try {
            tree = ConfigFile.ReadTree(path);
        } catch (ConfigMissingException) {
            return new UserContext();
        }
        List<ConfigError> errors = [.. tree.Errors];
        UserContext? context = errors.Count == 0 ? Read(tree.Root, Path.GetFileName(path), errors) : null;
        if (errors.Count > 0 || context is null) {
            throw new ConfigInvalidException(path, errors);
        }
        return context;
    }

    [UnsupportedOSPlatform("windows")]
    public static void Save(string path, UserContext context, ILogger logger) {
        List<ConfigError> errors = Validate(context, Path.GetFileName(path));
        if (errors.Count > 0) {
            throw new ConfigInvalidException(path, errors);
        }
        PrivateFile.Write(path, strictUtf8.GetBytes(YamlEmitter.Emit(ToTree(context), previous: null)), logger);
    }

    private static UserContext? Read(JsonNode? root, string fileName, List<ConfigError> errors) {
        if (root is not JsonObject values) {
            errors.Add(new ConfigError(fileName, ConfigProblem.NotSection));
            return null;
        }
        ConfigSection section = new(values, string.Empty, contextKeys, errors);
        UserContext context = new() {
            About = section.Text(AboutKey) ?? string.Empty,
            Docs = [.. (section.Sections(DocsKey, docKeys) ?? []).Select(ReadDoc)],
        };
        if (errors.Count > 0) {
            return null;
        }
        errors.AddRange(Validate(context, fileName));
        return errors.Count == 0 ? context : null;
    }

    private static ContextDoc ReadDoc(ConfigSection item) => new(
        item.RequiredText(IdKey) ?? string.Empty,
        item.RequiredText(NameKey) ?? string.Empty,
        item.RequiredText(TextKey, blankAllowed: true) ?? string.Empty,
        item.RequiredText(addedAtKey) ?? string.Empty);

    private static List<ConfigError> Validate(UserContext context, string fileName) {
        List<ConfigError> errors = [];
        ConfigSection.CheckLength(context.About, AboutKey, ContextEdits.AboutLimit, errors);
        if (context.Docs.Count > ContextEdits.DocsLimit) {
            errors.Add(new ConfigError(DocsKey, ConfigProblem.TooMany, LimitText(ContextEdits.DocsLimit)));
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < context.Docs.Count; index++) {
            ValidateDoc(context.Docs[index], ConfigLocation.Item(DocsKey, index), seen, errors);
        }
        if (context.TextLength > ContextEdits.TextLimit) {
            errors.Add(new ConfigError(fileName, ConfigProblem.TooLong, LimitText(ContextEdits.TextLimit)));
        }
        return errors;
    }

    private static void ValidateDoc(ContextDoc doc, string location, HashSet<string> seen, List<ConfigError> errors) {
        if (!ContextDoc.IsId(doc.Id)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, IdKey), ConfigProblem.NotDocId));
        } else if (!seen.Add(doc.Id)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, IdKey), ConfigProblem.DuplicateKey));
        }
        if (string.IsNullOrWhiteSpace(doc.Name)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, NameKey), ConfigProblem.Required));
        }
        ConfigSection.CheckLength(doc.Name, ConfigLocation.Join(location, NameKey), ContextDoc.NameLimit, errors);
        ConfigSection.CheckLength(doc.Text, ConfigLocation.Join(location, TextKey), ContextEdits.DocTextLimit, errors);
        if (!ContextDoc.IsDate(doc.AddedAt)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, addedAtKey), ConfigProblem.NotDate));
        }
    }

    private static JsonObject ToTree(UserContext context) => new() {
        [AboutKey] = context.About,
        [DocsKey] = new JsonArray([.. context.Docs.Select(doc => (JsonNode)new JsonObject {
            [IdKey] = doc.Id,
            [NameKey] = doc.Name,
            [TextKey] = doc.Text,
            [addedAtKey] = doc.AddedAt,
        })]),
    };

    private static string LimitText(int limit) => limit.ToString(CultureInfo.InvariantCulture);
}
