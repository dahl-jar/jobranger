using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Matching;

internal static class Matching {
    public static string LimitRules(Limits limits) => MatchRules.LimitRules(limits);

    public static double? MaxYears(Limits limits) => MatchRules.MaxYears(limits);

    public static string BuildSystem(string guidance, string limits, string profileText, string? contextText) =>
        SystemPrompt.Build(guidance, limits, profileText, contextText);

    public static string AssessmentVersion(string system) => SystemPrompt.Version(system);

    public static JsonObject SchemaFor(ProviderName provider) =>
        JsonNode.Parse(provider == ProviderName.Anthropic ? AssessmentSchema.Anthropic : AssessmentSchema.OpenAI)!.AsObject();

    public static PromptText BuildPrompt(AdView ad, string system, DateOnly date, IReadOnlyList<PromptExample> examples) =>
        ClassifyPrompts.Build(ad, system, date, examples);

    public static AdAssessment? ParseAssessment(string json) => AssessmentReader.Read(json);

    public static Classification? ToClassification(
        AdAssessment assessment,
        AdView ad,
        IReadOnlySet<string> evidence,
        IReadOnlySet<string> education,
        Limits limits,
        string version) =>
        Classifier.ToClassification(assessment, ad, evidence, education, limits, version);

    public static Classification WithTitleLevel(string title, Classification result) =>
        Classifier.WithTitleLevel(title, result);

    public static string? EncodeMatch(Match? match, IReadOnlyList<Check> checks) => StoredMatch.Encode(match, checks);

    public static Match? DecodeMatch(string? stored) => StoredMatch.Decode(stored);

    public static bool HasCurrentMatch(AdView ad, string version) => AdRules.HasCurrentMatch(ad, version);

    public static HiddenKind? HiddenReason(AdView ad, string version, Targets targets) =>
        AdRules.HiddenReason(ad, version, targets);

    public static bool IsReportable(AdView ad, string version, Targets targets) =>
        AdRules.HiddenReason(ad, version, targets) is null;

    public static DisplayFit DisplayFit(AdView ad, Targets targets) => AdRules.FitOf(ad, targets);

    public static bool IsStale(AdView ad, DateOnly today) => AdRules.IsStale(ad, today);

    public static DateOnly? ParseDate(string? value) => AdRules.ParseDate(value);

    public static AdView PreferredAd(IReadOnlyList<AdView> group, string version) => AdPreference.Preferred(group, version);

    public static GuidanceText LoadGuidance(string path) => Guidance.Load(path);

    [UnsupportedOSPlatform("windows")]
    public static GuidanceText SaveGuidance(string path, string text, ILogger logger) => Guidance.Save(path, text, logger);

    [UnsupportedOSPlatform("windows")]
    public static GuidanceText ResetGuidance(string path, ILogger logger) => Guidance.Reset(path, logger);
}
