using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.Loader;
using Jobranger.Cli.Tests.ArchitectureSeeds.Broken;
using Jobranger.Cli.Tests.ArchitectureSeeds.Sound;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

namespace Jobranger.Cli.Tests;

public sealed class ArchitectureTests {
    private const string cliRoot = "Jobranger.Cli";
    private const string featuresFolder = "Features";
    private const string commandsFolder = "Commands";
    private const string rootFolder = "";
    private const string anySlice = "(*)";
    private const string exceptionType = "System.Exception";
    private const char namespaceSeparator = '.';

    private static readonly string[] infrastructure =
        ["Catalogue", "Config", "Files", "Storage", "Http", "Secrets", "Logging", "Platform", "Text"];

    private static readonly Dictionary<string, string[]> allowedCalls = new() {
        ["Daemon"] = ["Delivery", "Setup", "Rechecks", "Runs"],
        ["Setup"] = ["DryRun", "Runs"],
        ["DryRun"] = ["Runs"],
        ["Delivery"] = ["Runs"],
        ["Rechecks"] = ["Runs"],
        ["Runs"] = [],
        ["Pairing"] = [],
    };

    private static readonly string[] orchestration = [.. allowedCalls.Keys];

    private static readonly Architecture cli =
        new ArchLoader().LoadAssemblies(typeof(CliServices).Assembly).Build();

    private static readonly Architecture seeds =
        new ArchLoader().LoadAssemblies(typeof(ArchitectureTests).Assembly).Build();

    private static readonly string brokenRoot = typeof(BrokenServices).Namespace!;

    private static readonly string soundRoot = typeof(SoundServices).Namespace!;

    [Fact]
    public void ShouldSeeCliTypes_WhenRulesLoadAssembly() {
        Assert.NotEmpty(Dependencies(cli, cliRoot));
    }

    [Theory]
    [InlineData("Features.Runs.Runs", "Features.Ads.Ads")]
    [InlineData("Features.Runs.Runs", "Features.Ads.Ad")]
    [InlineData("Features.Runs.Runs", "Features.Ads.AdKind")]
    [InlineData("Features.Runs.Runs", "Features.Ads.AdPosition")]
    [InlineData("Features.Runs.Runs", "Features.Ads.AdMissingException")]
    [InlineData("Features.Runs.Runs", "Features.Ads.IAdSource")]
    [InlineData("Features.Runs.Runs", "Features.Runs.RunClock")]
    [InlineData("Features.Ads.Ads", "Features.Ads.AdStore")]
    [InlineData("Features.Ads.Ads", "Http.SourceRequests")]
    [InlineData("Http.SourceRequests", "Text.Wording")]
    [InlineData("Features.Delivery.Delivery", "Features.Runs.Runs")]
    [InlineData("Commands.RunCommand", "Features.Delivery.Delivery")]
    public void ShouldSeeAllowedEdge_WhenSoundSeedUsesIt(string origin, string target) {
        string[] edges = [.. Dependencies(seeds, soundRoot).Select(Describe)];

        Assert.Contains($"{soundRoot}.{origin} -> {soundRoot}.{target}", edges);
    }

    [Fact]
    public void ShouldReferenceNoFeature_WhenTypeIsInfrastructure() {
        Assert.Empty(InfrastructureViolations(cli, cliRoot));
    }

    [Fact]
    public void ShouldReferenceOnlyInfrastructure_WhenFeatureIsLeaf() {
        Assert.Empty(LeafViolations(cli, cliRoot));
    }

    [Fact]
    public void ShouldCallOnlyEntryType_WhenOrchestrationOrCommandUsesFeature() {
        Assert.Empty(EntryTypeViolations(cli, cliRoot));
    }

    [Fact]
    public void ShouldFindNoCycle_WhenFeaturesCallEachOther() {
        Assert.Empty(CycleViolations(cli, cliRoot));
    }

    [Fact]
    public void ShouldCallOnlyAllowedOrchestration_WhenOrchestrationCallsOrchestration() {
        Assert.Empty(OrchestrationEdgeViolations(cli, cliRoot));
    }

    [Theory]
    [InlineData("Runs", "Delivery")]
    [InlineData("Runs", "Setup")]
    [InlineData("Pairing", "Runs")]
    public void ShouldReportViolation_WhenOrchestrationCallsForbiddenFeature(string origin, string target) {
        string[] violations = OrchestrationEdgeViolations(seeds, brokenRoot);

        Assert.Contains($"{origin} -> {target}", violations);
    }

    [Fact]
    public void ShouldReportViolation_WhenInfrastructureTakesFeatureRecord() {
        string[] violations = InfrastructureViolations(seeds, brokenRoot);

        Assert.Contains($"{brokenRoot}.Http.SourceRequests -> {brokenRoot}.Features.Ads.Ad", violations);
    }

    [Fact]
    public void ShouldReportViolation_WhenLeafFeatureTakesOtherFeatureRecord() {
        string[] violations = LeafViolations(seeds, brokenRoot);

        Assert.Contains($"{brokenRoot}.Features.Matching.Matching -> {brokenRoot}.Features.Ads.Ad", violations);
    }

    [Fact]
    public void ShouldReportViolation_WhenLeafFeatureTakesRootType() {
        string[] violations = LeafViolations(seeds, brokenRoot);

        Assert.Contains($"{brokenRoot}.Features.Matching.Matching -> {brokenRoot}.BrokenServices", violations);
    }

    [Theory]
    [InlineData("Features.Runs.RunPass")]
    [InlineData("Commands.RunCommand")]
    public void ShouldReportViolation_WhenOrchestrationOrCommandCallsFeatureStore(string origin) {
        string[] violations = EntryTypeViolations(seeds, brokenRoot);

        Assert.Contains($"{brokenRoot}.{origin} -> {brokenRoot}.Features.Ads.AdStore", violations);
    }

    [Fact]
    public void ShouldReportViolation_WhenFeaturesCallEachOtherBothWays() {
        string[] violations = CycleViolations(seeds, brokenRoot);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void ShouldReportNothing_WhenSeedFollowsEveryRule() {
        string[] violations = [
            .. InfrastructureViolations(seeds, soundRoot),
            .. LeafViolations(seeds, soundRoot),
            .. EntryTypeViolations(seeds, soundRoot),
            .. CycleViolations(seeds, soundRoot),
            .. OrchestrationEdgeViolations(seeds, soundRoot),
        ];

        Assert.Empty(violations);
    }

    private static string[] InfrastructureViolations(Architecture architecture, string root) =>
        [.. Dependencies(architecture, root)
            .Where(dependency => infrastructure.Contains(Folder(dependency.Origin, root)))
            .Where(dependency => Feature(dependency.Target, root) is not null)
            .Select(Describe)];

    private static string[] LeafViolations(Architecture architecture, string root) =>
        [.. Dependencies(architecture, root)
            .Where(dependency => Feature(dependency.Origin, root) is string origin && !orchestration.Contains(origin))
            .Where(dependency => !infrastructure.Contains(Folder(dependency.Target, root)))
            .Where(dependency => Feature(dependency.Target, root) != Feature(dependency.Origin, root))
            .Select(Describe)];

    private static string[] EntryTypeViolations(Architecture architecture, string root) =>
        [.. Dependencies(architecture, root)
            .Where(dependency => IsOrchestrationOrCommand(dependency.Origin, root))
            .Where(dependency => Feature(dependency.Target, root) is string target
                && target != Feature(dependency.Origin, root))
            .Where(dependency => !IsEntryType(dependency.Target, root) && !IsSharedType(dependency.Target))
            .Select(Describe)];

    private static string[] OrchestrationEdgeViolations(Architecture architecture, string root) =>
        [.. Dependencies(architecture, root)
            .Select(dependency => (Origin: Feature(dependency.Origin, root), Target: Feature(dependency.Target, root)))
            .Where(edge => edge.Origin is not null && orchestration.Contains(edge.Origin))
            .Where(edge => edge.Target is not null && orchestration.Contains(edge.Target))
            .Where(edge => edge.Origin != edge.Target)
            .Where(edge => !allowedCalls[edge.Origin!].Contains(edge.Target))
            .Select(edge => $"{edge.Origin} -> {edge.Target}")
            .Distinct()];

    private static string[] CycleViolations(Architecture architecture, string root) {
        SliceRule rule = Slices()
            .Matching(FeaturesPrefix(root) + anySlice)
            .Should().BeFreeOfCycles();
        return [.. rule.Evaluate(architecture)
            .Where(result => !result.Passed)
            .Select(result => result.Description)];
    }

    private static IEnumerable<ITypeDependency> Dependencies(Architecture architecture, string root) =>
        architecture.Types
            .Where(type => Folder(type, root) is not null)
            .SelectMany(type => type.Dependencies)
            .Where(dependency => Folder(dependency.Target, root) is not null);

    private static string? Folder(IType type, string root) =>
        NamespaceOf(type) == root ? rootFolder : SegmentAfter(type, root + namespaceSeparator);

    private static string? Feature(IType type, string root) =>
        SegmentAfter(type, FeaturesPrefix(root));

    private static string? SegmentAfter(IType type, string prefix) {
        string name = NamespaceOf(type);
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) {
            return null;
        }
        return name[prefix.Length..].Split(namespaceSeparator)[0];
    }

    private static string FeaturesPrefix(string root) =>
        root + namespaceSeparator + featuresFolder + namespaceSeparator;

    private static string NamespaceOf(IType type) => type.Namespace?.FullName ?? string.Empty;

    private static bool IsOrchestrationOrCommand(IType type, string root) =>
        Folder(type, root) == commandsFolder || orchestration.Contains(Feature(type, root));

    private static bool IsEntryType(IType type, string root) =>
        Feature(type, root) is string feature
        && type.Name == feature
        && NamespaceOf(type) == FeaturesPrefix(root) + feature;

    private static bool IsSharedType(IType type) =>
        type is Struct or ArchUnitNET.Domain.Enum or Interface
        || type is Class { IsRecord: true }
        || type is Class exception && exception.InheritedClasses.Any(parent => parent.FullName == exceptionType);

    private static string Describe(ITypeDependency dependency) =>
        $"{dependency.Origin.FullName} -> {dependency.Target.FullName}";
}
