using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class AdPreferenceTests {
    private const string applyUrl = "https://jobs.example.test/apply";
    private const string expired = "expired";

    private static readonly AdView ad = MatchingFixtures.Ad;
    private static readonly Match match = MatchingFixtures.StrongMatch;
    private static readonly Match stale = match with { Version = MatchingFixtures.OutdatedVersion };

    private static readonly IReadOnlyDictionary<string, Match> blockedMatches = new Dictionary<string, Match> {
        ["unsuitable"] = match with { Status = "unsuitable" },
        ["blockers"] = match with { Status = "stretch", Blockers = ["SAP required"] },
        ["low code"] = match with { IsLowCode = true },
    };

    public static TheoryData<string> BlockedMatches() => [.. blockedMatches.Keys];

    [Fact]
    public void ShouldMergeGroupFields_WhenMembersDiffer() {
        AdView classified = ad with {
            Id = "nav:1",
            Source = "nav",
            Description = "Short.",
            Summary = "Backend work.",
            DisplayTitle = "Backend Developer",
            RoleType = "backend",
            Technologies = ["Java"],
            Requirements = ["Bachelor"],
            RequiresMaster = true,
            Deadline = "2026-11-02",
            Published = "2026-10-02",
            Label = "junior",
        };
        AdView direct = ad with {
            Id = "ashby:org:1",
            Source = "ashby:org",
            Description = "A much longer description of the same role.",
            DisplayTitle = null,
            Match = stale,
            Deadline = "2026-11-01",
            Published = "2026-10-03",
            ApplyUrl = applyUrl,
            ApplyStatus = expired,
            Republished = true,
            Label = "junior",
        };

        AdView merged = MatchingEntry.PreferredAd([classified, direct], MatchingFixtures.Version);

        Assert.Equal(
            ("ashby:org:1", "A much longer description of the same role.", "Backend work.", "Backend Developer", "backend",
                "Java", "Bachelor", true, true, match, "junior", "2026-11-01", "2026-10-02", true, expired, applyUrl),
            (merged.Id, merged.Description, merged.Summary, merged.DisplayTitle, merged.RoleType,
                merged.Technologies.Single(), merged.Requirements.Single(), merged.RequiresMaster, merged.IsProgrammingJob,
                merged.Match, merged.Label, merged.Deadline, merged.Published, merged.Republished, merged.ApplyStatus,
                merged.ApplyUrl));
    }

    [Fact]
    public void ShouldTakeFirstApplyUrl_WhenSelectedAdHasNone() {
        AdView selected = ad with { Id = "ashby:org:1", Source = "ashby:org" };
        AdView other = ad with { Id = "finn:1", ApplyUrl = applyUrl };

        Assert.Equal(applyUrl, MatchingEntry.PreferredAd([selected, other], MatchingFixtures.Version).ApplyUrl);
    }

    [Fact]
    public void ShouldKeepSelectedApplyLink_WhenNoMemberIsExpired() {
        AdView other = ad with { Id = "finn:1", ApplyUrl = applyUrl };
        AdView selected = ad with {
            Id = "ashby:org:1",
            Source = "ashby:org",
            ApplyUrl = "https://a.example.test",
            ApplyStatus = "open",
        };

        AdView merged = MatchingEntry.PreferredAd([other, selected], MatchingFixtures.Version);

        Assert.Equal(("https://a.example.test", "open"), (merged.ApplyUrl, merged.ApplyStatus));
    }

    [Fact]
    public void ShouldSelectAdWithDescription_WhenOtherHasNone() {
        AdView described = ad with { Id = "finn:1", Description = "Short." };
        AdView empty = ad with { Id = "finn:2", Description = null };

        AdView merged = MatchingEntry.PreferredAd([described, empty], MatchingFixtures.Version);

        Assert.Equal(("finn:1", "Short."), (merged.Id, merged.Description));
    }

    [Fact]
    public void ShouldPreferClassification_WhenItListsMoreItems() {
        AdView fewer = ad with { Id = "finn:1", Technologies = ["Java"] };
        AdView more = ad with {
            Id = "finn:2",
            Technologies = ["Java", "Kotlin"],
            Requirements = ["Bachelor", "Norwegian"],
            Summary = "More detail.",
        };

        Assert.Equal("More detail.", MatchingEntry.PreferredAd([fewer, more], MatchingFixtures.Version).Summary);
    }

    [Fact]
    public void ShouldPreferClassification_WhenItHasDisplayTitle() {
        AdView untitled = ad with { Id = "finn:1", DisplayTitle = null, Summary = "Untitled." };
        AdView titled = ad with { Id = "finn:2", DisplayTitle = "Developer", Summary = "Titled." };

        Assert.Equal("Titled.", MatchingEntry.PreferredAd([untitled, titled], MatchingFixtures.Version).Summary);
    }

    [Fact]
    public void ShouldTakeFieldsFromOtherMembers_WhenSelectedAdLacksThem() {
        AdView selected = ad with {
            Id = "ashby:org:1",
            Source = "ashby:org",
            Description = "Short.",
            Label = null,
            IsProgrammingJob = null,
            Match = stale,
            Deadline = "2026-11-05",
        };
        AdView other = ad with {
            Id = "finn:1",
            Description = "A much longer description.",
            Label = "lead",
            Deadline = "2026-11-01",
            Republished = true,
            ApplyStatus = expired,
        };

        AdView merged = MatchingEntry.PreferredAd([selected, other], MatchingFixtures.Version);

        Assert.Equal(
            ("ashby:org:1", "A much longer description.", "lead", (bool?)true, "2026-11-01", true, expired),
            (merged.Id, merged.Description, merged.Label, merged.IsProgrammingJob, merged.Deadline, merged.Republished,
                merged.ApplyStatus));
    }

    [Fact]
    public void ShouldTakeLeadLabel_WhenAnyMemberHasIt() {
        AdView classified = ad with { Id = "finn:1", Label = "junior" };
        AdView senior = ad with { Id = "finn:2", Label = "senior", Match = stale };
        AdView lead = ad with { Id = "finn:3", Label = "lead", Match = stale };

        Assert.Equal("lead", MatchingEntry.PreferredAd([classified, senior, lead], MatchingFixtures.Version).Label);
    }

    [Fact]
    public void ShouldTakeSeniorLabel_WhenNoMemberIsLead() {
        AdView classified = ad with { Id = "finn:1", Label = "junior" };
        AdView senior = ad with { Id = "finn:2", Label = "senior", Match = stale };

        Assert.Equal("senior", MatchingEntry.PreferredAd([classified, senior], MatchingFixtures.Version).Label);
    }

    [Fact]
    public void ShouldPreferCurrentMatch_WhenOtherMatchIsStale() {
        AdView old = ad with { Id = "finn:1", Match = stale, Technologies = ["Java", "Kotlin", "Go"] };
        AdView current = ad with { Id = "finn:2" };

        Assert.Equal(match, MatchingEntry.PreferredAd([old, current], MatchingFixtures.Version).Match);
    }

    [Theory]
    [MemberData(nameof(BlockedMatches))]
    public void ShouldPreferBlockedMatch_WhenAnyMemberHasOne(string name) {
        Match blocked = blockedMatches[name];
        AdView safe = ad with { Id = "finn:1", Technologies = ["Java", "Kotlin"] };
        AdView other = ad with { Id = "nav:1", Source = "nav", Match = blocked };

        Assert.Equal(blocked, MatchingEntry.PreferredAd([safe, other], MatchingFixtures.Version).Match);
    }
}
