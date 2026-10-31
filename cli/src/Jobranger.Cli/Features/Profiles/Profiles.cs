using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Config.Yaml;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Profiles;

internal static class Profiles {
    private const char indentStep = ' ';
    private const char fieldSeparator = ',';

    public static Profile Load(string path) {
        YamlTree tree = ConfigFile.ReadTree(path);
        List<ConfigError> errors = [.. tree.Errors];
        Profile? profile = errors.Count == 0 ? ProfileFile.Read(tree, Path.GetFileName(path), errors) : null;
        if (errors.Count > 0 || profile is null) {
            throw new ConfigInvalidException(path, errors);
        }
        return profile;
    }

    public static string Text(Profile profile) {
        StringBuilder json = new();
        AppendObject(json, 0, [
            (ProfileFile.SummaryKey, (text, _) => PythonJson.AppendString(text, profile.Summary)),
            (ProfileFile.YearsKey, (text, _) => text.Append(Years(profile.ProfessionalYears))),
            (ProfileFile.HasMasterKey, (text, _) => text.Append(PythonJson.Bool(profile.HasMaster))),
            (ProfileFile.EducationKey, (text, level) => AppendEvidence(text, level, profile.Education)),
            (ProfileFile.ExperienceKey, (text, level) => AppendEvidence(text, level, profile.Experience)),
            (ProfileFile.ProjectsKey, (text, level) => AppendEvidence(text, level, profile.Projects)),
            (ProfileFile.LanguagesKey, (text, level) => AppendStrings(text, level, profile.Languages)),
            (ProfileFile.StackKey, (text, level) => AppendStrings(text, level, profile.Stack)),
        ]);
        return json.ToString();
    }

    public static IReadOnlySet<string> EvidenceIds(Profile profile) =>
        new HashSet<string>(
            profile.Education.Concat(profile.Experience).Concat(profile.Projects).Select(item => item.Id),
            StringComparer.Ordinal);

    public static IReadOnlySet<string> EducationIds(Profile profile) =>
        new HashSet<string>(profile.Education.Select(item => item.Id), StringComparer.Ordinal);

    public static IReadOnlySet<string> CandidateEvidence(Profile profile, UserContext context, string contextPath) {
        IReadOnlySet<string> profileIds = EvidenceIds(profile);
        IReadOnlySet<string> contextIds = ContextEdits.Ids(context);
        List<ConfigError> clashes = [
            .. contextIds.Where(profileIds.Contains).Order(StringComparer.Ordinal)
                .Select(id => new ConfigError(id, ConfigProblem.DuplicateKey)),
        ];
        if (clashes.Count > 0) {
            throw new ConfigInvalidException(contextPath, clashes);
        }
        return new HashSet<string>(profileIds.Concat(contextIds), StringComparer.Ordinal);
    }

    public static UserContext LoadContext(string path) => ContextFile.Load(path);

    [UnsupportedOSPlatform("windows")]
    public static void SaveContext(string path, UserContext context, ILogger logger) =>
        ContextFile.Save(path, context, logger);

    public static UserContext WithAbout(UserContext context, string text) => ContextEdits.WithAbout(context, text);

    public static UserContext WithDoc(UserContext context, string name, string text, DateOnly today) =>
        ContextEdits.WithDoc(context, name, text, today);

    public static UserContext WithoutDoc(UserContext context, string id) => ContextEdits.WithoutDoc(context, id);

    public static IReadOnlySet<string> ContextIds(UserContext context) => ContextEdits.Ids(context);

    public static string? ContextText(UserContext context) {
        if (ContextEdits.Ids(context).Count == 0) {
            return null;
        }
        StringBuilder json = new();
        AppendObject(json, 0, [
            (ContextFile.AboutKey, (text, _) => PythonJson.AppendString(text, context.About)),
            (ContextFile.DocsKey, (text, level) => AppendItems(text, level, '[', ']', context.Docs, AppendDoc)),
        ]);
        return json.ToString();
    }

    private static void AppendEvidence(StringBuilder json, int depth, IReadOnlyList<Evidence> items) =>
        AppendItems(json, depth, '[', ']', items, (text, level, item) => AppendObject(text, level, [
            (ProfileFile.IdKey, (field, _) => PythonJson.AppendString(field, item.Id)),
            (ProfileFile.NameKey, (field, _) => PythonJson.AppendString(field, item.Name)),
            (ProfileFile.FactsKey, (field, _) => PythonJson.AppendString(field, item.Facts)),
            (ProfileFile.StartKey, (field, _) => AppendOptional(field, item.Start)),
            (ProfileFile.EndKey, (field, _) => AppendOptional(field, item.End)),
        ]));

    private static void AppendDoc(StringBuilder json, int depth, ContextDoc doc) =>
        AppendObject(json, depth, [
            (ContextFile.IdKey, (field, _) => PythonJson.AppendString(field, doc.Id)),
            (ContextFile.NameKey, (field, _) => PythonJson.AppendString(field, doc.Name)),
            (ContextFile.TextKey, (field, _) => PythonJson.AppendString(field, doc.Text)),
        ]);

    private static void AppendStrings(StringBuilder json, int depth, IReadOnlyList<string> items) =>
        AppendItems(json, depth, '[', ']', items, (text, _, item) => PythonJson.AppendString(text, item));

    private static string Years(double years) => double.IsFinite(years) ? PythonJson.Float(years) : PythonJson.Null;

    private static void AppendOptional(StringBuilder json, string? text) {
        if (text is null) {
            json.Append(PythonJson.Null);
        } else {
            PythonJson.AppendString(json, text);
        }
    }

    private static void AppendObject(
        StringBuilder json, int depth, IReadOnlyList<(string Key, Action<StringBuilder, int> Value)> fields) =>
        AppendItems(json, depth, '{', '}', fields, (text, level, field) => {
            PythonJson.AppendString(text, field.Key).Append(PythonJson.KeySeparator);
            field.Value(text, level);
        });

    private static void AppendItems<T>(
        StringBuilder json, int depth, char open, char close, IReadOnlyList<T> items, Action<StringBuilder, int, T> append) {
        json.Append(open);
        if (items.Count == 0) {
            json.Append(close);
            return;
        }
        for (int index = 0; index < items.Count; index++) {
            NewLine(json, depth + 1);
            append(json, depth + 1, items[index]);
            if (index < items.Count - 1) {
                json.Append(fieldSeparator);
            }
        }
        NewLine(json, depth);
        json.Append(close);
    }

    private static void NewLine(StringBuilder json, int depth) => json.Append('\n').Append(indentStep, depth);
}
