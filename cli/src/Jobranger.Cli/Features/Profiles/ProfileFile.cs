using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using Jobranger.Cli.Config.Yaml;

namespace Jobranger.Cli.Features.Profiles;

internal static partial class ProfileFile {
    public const string SummaryKey = "summary";
    public const string YearsKey = "professional_years";
    public const string HasMasterKey = "has_master";
    public const string EducationKey = "education";
    public const string ExperienceKey = "experience";
    public const string ProjectsKey = "projects";
    public const string LanguagesKey = "languages";
    public const string StackKey = "stack";
    public const string IdKey = "id";
    public const string NameKey = "name";
    public const string FactsKey = "facts";
    public const string StartKey = "start";
    public const string EndKey = "end";

    private const int factsLimit = 1200;

    private static readonly string[] profileKeys =
        [SummaryKey, YearsKey, HasMasterKey, EducationKey, ExperienceKey, ProjectsKey, LanguagesKey, StackKey];
    private static readonly string[] evidenceKeys = [IdKey, NameKey, FactsKey, StartKey, EndKey];
    private static readonly string[] evidenceLists = [EducationKey, ExperienceKey, ProjectsKey];

    public static Profile? Read(YamlTree tree, string fileName, List<ConfigError> errors) {
        if (tree.Root is not JsonObject values) {
            errors.Add(new ConfigError(fileName, ConfigProblem.NotSection));
            return null;
        }
        ConfigSection section = new(values, string.Empty, profileKeys, errors, tree.Scalars);
        string? summary = section.StrictText(SummaryKey);
        double? years = section.LaxNumber(YearsKey);
        if (years is double value && !(value >= 0)) {
            errors.Add(new ConfigError(YearsKey, ConfigProblem.BelowZero));
        }
        Dictionary<string, List<Evidence>> lists = evidenceLists.ToDictionary(
            key => key, key => EvidenceList(section, key, errors), StringComparer.Ordinal);
        CheckUniqueIds(lists, errors);
        Profile profile = new() {
            Summary = summary ?? string.Empty,
            ProfessionalYears = years ?? 0,
            HasMaster = section.LaxFlag(HasMasterKey) ?? false,
            Education = lists[EducationKey],
            Experience = lists[ExperienceKey],
            Projects = lists[ProjectsKey],
            Languages = section.StrictTextList(LanguagesKey) ?? [],
            Stack = section.StrictTextList(StackKey) ?? [],
        };
        return errors.Count == 0 ? profile : null;
    }

    private static List<Evidence> EvidenceList(ConfigSection section, string key, List<ConfigError> errors) {
        List<ConfigSection> items = section.Sections(key, evidenceKeys) ?? [];
        List<Evidence> evidence = [];
        for (int index = 0; index < items.Count; index++) {
            string location = ConfigLocation.Item(key, index);
            evidence.Add(ReadEvidence(items[index], location, errors));
        }
        return evidence;
    }

    private static Evidence ReadEvidence(ConfigSection item, string location, List<ConfigError> errors) {
        string? id = item.StrictText(IdKey);
        if (id is not null && !EvidenceIdPattern().IsMatch(id)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, IdKey), ConfigProblem.NotEvidenceId));
        } else if (id is not null && ContextEdits.IsReserved(id)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, IdKey), ConfigProblem.ReservedId));
        }
        string facts = item.StrictText(FactsKey) ?? string.Empty;
        ConfigSection.CheckLength(facts, ConfigLocation.Join(location, FactsKey), factsLimit, errors);
        return new Evidence(
            id ?? string.Empty,
            item.StrictText(NameKey) ?? string.Empty,
            facts,
            Month(item, location, StartKey, errors),
            Month(item, location, EndKey, errors));
    }

    private static string? Month(ConfigSection item, string location, string key, List<ConfigError> errors) {
        string? month = item.OptionalText(key);
        if (month is not null && !MonthPattern().IsMatch(month)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, key), ConfigProblem.NotMonth));
        }
        return month;
    }

    private static void CheckUniqueIds(Dictionary<string, List<Evidence>> lists, List<ConfigError> errors) {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string key in evidenceLists) {
            List<Evidence> items = lists[key];
            for (int index = 0; index < items.Count; index++) {
                if (items[index].Id.Length > 0 && !seen.Add(items[index].Id)) {
                    errors.Add(new ConfigError(
                        ConfigLocation.Join(ConfigLocation.Item(key, index), IdKey), ConfigProblem.DuplicateKey));
                }
            }
        }
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,39}\z")]
    private static partial Regex EvidenceIdPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}\z")]
    private static partial Regex MonthPattern();
}
