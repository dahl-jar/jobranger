using System.Text.Json;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class AdRulesTests {
    private static readonly DateOnly today = new(2026, 10, 9);
    private static readonly AdView ad = MatchingFixtures.Ad;
    private static readonly Match match = MatchingFixtures.StrongMatch;

    private static readonly IReadOnlyDictionary<string, Targets> goldenTargets = new Dictionary<string, Targets> {
        ["narrow"] = MatchingFixtures.AllRoles(),
        ["wide"] = new() { Subfields = ["backend"], Limits = MatchingFixtures.Wide, Places = ["Oslo"], Relocate = false },
    };

    private static readonly IReadOnlyDictionary<string, (AdView Ad, HiddenKind Kind)> hiddenCases =
        new Dictionary<string, (AdView, HiddenKind)> {
            ["not programming"] = (ad with { IsProgrammingJob = false, RoleType = null }, HiddenKind.Other),
            ["not programming with role"] = (ad with { IsProgrammingJob = false, RoleType = "backend" }, HiddenKind.Other),
            ["role"] = (ad with { RoleType = "backend" }, HiddenKind.Role),
            ["masters"] = (ad with { RequiresMaster = true }, HiddenKind.Masters),
            ["masters before fit"] = (ad with { RequiresMaster = true, Match = null }, HiddenKind.Masters),
            ["place before fit"] = (ad with { Location = "Oslo", Match = null }, HiddenKind.Place),
            ["no match"] = (ad with { Match = null }, HiddenKind.Other),
            ["low code"] = (ad with { Match = match with { IsLowCode = true } }, HiddenKind.Other),
            ["unknown"] = (ad with { Match = match with { Status = "unknown" } }, HiddenKind.Other),
            ["outdated version"] = (ad with { Match = match with { Version = MatchingFixtures.OutdatedVersion } }, HiddenKind.Other),
            ["blocked"] = (ad with { Match = match with { Status = "unsuitable", Blockers = ["SAP required"] } }, HiddenKind.Other),
            ["level"] = (ad with { Label = "lead", Match = match with { RequiredExperienceYears = 8 } }, HiddenKind.Senior),
            ["years"] = (ad with { Match = match with { RequiredExperienceYears = 2.5 } }, HiddenKind.Years),
            ["area before years"] = (ad with { IsProgrammingJob = false, RoleType = null, Match = match with { RequiredExperienceYears = 5 } }, HiddenKind.Other),
        };

    private static readonly IReadOnlyDictionary<string, (AdView Ad, DisplayFit Fit)> displayCases =
        new Dictionary<string, (AdView, DisplayFit)> {
            ["strong in place"] = (ad, DisplayFit.Strong),
            ["stretch"] = (ad with { Match = match with { Status = "stretch", Gaps = ["Qt"] } }, DisplayFit.Partial),
            ["no match"] = (ad with { Match = null }, DisplayFit.Partial),
            ["outside places"] = (ad with { Location = "Oslo" }, DisplayFit.Partial),
            ["unsuitable"] = (ad with { Match = match with { Status = "unsuitable" } }, DisplayFit.OnRequest),
            ["unknown"] = (ad with { Match = match with { Status = "unknown" } }, DisplayFit.OnRequest),
        };

    public static TheoryData<string> CaseIds() => [.. MatchingCases.All().Keys];

    public static TheoryData<string> HiddenCases() => [.. hiddenCases.Keys];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ShouldMatchPython_WhenRulesRunOnMatchingCases(string caseId) {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml"));
        AdView caseAd = MatchingCases.Ad(MatchingCases.All()[caseId]);
        Classification result = MatchingEntry.ToClassification(
            MatchingEntry.ParseAssessment(MatchingCases.Replies()[caseId])!,
            caseAd,
            ProfilesEntry.EvidenceIds(profile),
            ProfilesEntry.EducationIds(profile),
            MatchingFixtures.Narrow,
            MatchingFixtures.Version)!;
        AdView classified = caseAd with {
            Label = result.Label,
            Summary = result.Summary,
            RequiresMaster = result.RequiresMaster,
            IsProgrammingJob = result.IsProgrammingJob,
            DisplayTitle = result.DisplayTitle,
            RoleType = result.RoleType,
            Technologies = result.Technologies,
            Requirements = result.Requirements,
            Match = result.Match,
        };
        using JsonDocument golden = JsonDocument.Parse(MatchingFixtures.GoldenText("Rules", "rules.json"));

        string observed = string.Join('|', goldenTargets.Select(pair => Row(classified, pair.Value)));

        string expected = string.Join(
            '|', goldenTargets.Keys.Select(name => GoldenJson.Canonical(golden.RootElement.GetProperty(caseId).GetProperty(name))));
        Assert.Equal(expected, observed);
    }

    [Theory]
    [MemberData(nameof(HiddenCases))]
    public void ShouldGiveHiddenKind_WhenAdFailsRule(string name) {
        (AdView hiddenAd, HiddenKind kind) = hiddenCases[name];
        Targets targets = MatchingFixtures.AllRoles() with { Subfields = ["frontend"], Places = ["Bergen"], Relocate = false };

        HiddenKind? hidden = MatchingEntry.HiddenReason(hiddenAd, MatchingFixtures.Version, targets);

        Assert.Equal(kind, hidden);
    }

    [Theory]
    [InlineData("backend", true)]
    [InlineData("frontend", false)]
    public void ShouldReportAd_WhenRoleIsInSubfields(string subfield, bool reportable) {
        Targets targets = MatchingFixtures.AllRoles() with { Subfields = [subfield] };

        Assert.Equal(reportable, MatchingEntry.IsReportable(ad with { RoleType = "backend" }, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldReportGeneralSoftwareAd_WhenNoSubfieldIsSelected() {
        Targets targets = MatchingFixtures.AllRoles() with { Subfields = [] };

        Assert.True(MatchingEntry.IsReportable(ad, MatchingFixtures.Version, targets));
    }

    [Theory]
    [InlineData("Oslo", false)]
    [InlineData("Mo i Rana kommune", true)]
    [InlineData("", true)]
    [InlineData("MO I  RANA", true)]
    [InlineData("Mo i Ranaby", false)]
    public void ShouldMatchPlace_WhenRelocateIsOff(string location, bool reportable) {
        Targets targets = MatchingFixtures.AllRoles() with { Places = ["Mo i Rana"], Relocate = false };

        Assert.Equal(reportable, MatchingEntry.IsReportable(ad with { Location = location }, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldReportAd_WhenSecondPlaceMatches() {
        Targets targets = MatchingFixtures.AllRoles() with { Places = ["Bergen", "Mo i Rana"], Relocate = false };

        Assert.True(MatchingEntry.IsReportable(ad with { Location = "Mo i Rana" }, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldReportAdOutsidePlaces_WhenRelocateIsOn() {
        Targets targets = MatchingFixtures.AllRoles() with { Places = ["Bergen"], Relocate = true };

        Assert.True(MatchingEntry.IsReportable(ad with { Location = "Oslo" }, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldHideAd_WhenYearsFallOutsideSelectedBands() {
        Targets targets = MatchingFixtures.AllRoles(new Limits { Years = ["0-1", "1-3", "6+"] });

        bool result = MatchingEntry.IsReportable(
            ad with { Match = match with { RequiredExperienceYears = 4.0 } }, MatchingFixtures.Version, targets);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReportAd_WhenMatchStatesNoYears() {
        Targets targets = MatchingFixtures.AllRoles(new Limits { Years = ["0-1", "1-3", "6+"] });

        Assert.True(MatchingEntry.IsReportable(ad, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldReportSeniorAd_WhenSeniorLevelIsSelected() {
        Targets targets = MatchingFixtures.AllRoles(new Limits { Levels = ["senior"] });

        Assert.True(MatchingEntry.IsReportable(ad with { Label = "senior" }, MatchingFixtures.Version, targets));
    }

    [Fact]
    public void ShouldReportMasterAd_WhenHideMastersIsOff() {
        Targets targets = MatchingFixtures.AllRoles(new Limits { HideMasters = false });

        Assert.True(MatchingEntry.IsReportable(ad with { RequiresMaster = true }, MatchingFixtures.Version, targets));
    }

    [Theory]
    [InlineData("2026-07-10T00:00:00+00:00", true)]
    [InlineData("2026-07-11", false)]
    [InlineData(null, false)]
    [InlineData("Snarest", false)]
    public void ShouldMarkAdStale_WhenPublishedOverNinetyDaysAgo(string? published, bool stale) {
        Assert.Equal(stale, MatchingEntry.IsStale(ad with { Published = published }, today));
    }

    public static TheoryData<string> DisplayCases() => [.. displayCases.Keys];

    [Theory]
    [MemberData(nameof(DisplayCases))]
    public void ShouldGiveDisplayFit_WhenMatchStatusOrPlaceDiffers(string name) {
        (AdView shown, DisplayFit fit) = displayCases[name];

        DisplayFit result = MatchingEntry.DisplayFit(shown, MatchingFixtures.AllRoles() with { Places = ["Bergen"] });

        Assert.Equal(fit, result);
    }

    [Theory]
    [InlineData(MatchingFixtures.Version, true)]
    [InlineData(MatchingFixtures.OutdatedVersion, false)]
    public void ShouldFindCurrentMatch_WhenVersionMatches(string version, bool current) {
        Assert.Equal(current, MatchingEntry.HasCurrentMatch(ad, version));
    }

    private static string Row(AdView classified, Targets targets) {
        HiddenKind? hidden = MatchingEntry.HiddenReason(classified, MatchingFixtures.Version, targets);
        string fit = MatchingEntry.DisplayFit(classified, targets) switch {
            DisplayFit.Strong => "strong",
            DisplayFit.Partial => "partial",
            _ => "on_request",
        };
        return GoldenJson.Row(
            ("display_fit", fit),
            ("hidden", hidden?.ToString().ToLowerInvariant()),
            ("reportable", MatchingEntry.IsReportable(classified, MatchingFixtures.Version, targets)));
    }
}
