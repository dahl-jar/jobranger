using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Runs;

public sealed class RunConversionsTests {
    private const string adUrl = "https://jobs.example.test/ad/1234";
    private const int hiddenCount = 2;

    [Fact]
    public void ShouldKeepMatchChecks_WhenStoringClassification() {
        Check[] checks = [new("Java services", "met")];
        Classification classification = new(
            "junior", null, false, true, null, "backend", [], [], MatchingFixtures.StrongMatch, checks);

        StoredClassification stored = RunConversions.ToStored(classification);

        Assert.Equal(MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, checks), stored.Assessment);
    }

    [Fact]
    public void ShouldCountZeroTokens_WhenReplyHasNoUsage() {
        TokenCounts tokens = RunConversions.ToTokens(null);

        Assert.Equal(new TokenCounts(0, 0, 0, 0, 0), tokens);
    }

    [Fact]
    public void ShouldCopyAdFields_WhenCardIsBuilt() {
        AdView view = MatchingFixtures.Ad with { Deadline = "2026-10-12", Summary = "Build Java services." };

        DigestCard card = RunConversions.ToDigestCard(view, adUrl, MatchingFixtures.AllRoles());

        Assert.Equal(
            new DigestCard {
                Id = "finn:1234",
                Source = "finn",
                Title = "Utvikler",
                DisplayTitle = "Software Developer",
                Company = "Org One AS",
                Location = "Bergen",
                Deadline = "2026-10-12",
                DeadlineDate = new DateOnly(2026, 10, 12),
                Url = adUrl,
                Summary = "Build Java services.",
                RoleLabel = "Software",
                Fit = DigestFit.Strong,
                IsStrongMatch = true,
            },
            card);
    }

    [Theory]
    [InlineData("strong", "Strong")]
    [InlineData("stretch", "Partial")]
    [InlineData("unsuitable", "OnRequest")]
    public void ShouldMapDisplayFit_WhenCardIsBuilt(string status, string fit) {
        AdView view = MatchingFixtures.Ad with { Match = MatchingFixtures.StrongMatch with { Status = status } };

        DigestCard card = RunConversions.ToDigestCard(view, adUrl, MatchingFixtures.AllRoles());

        Assert.Equal(Enum.Parse<DigestFit>(fit), card.Fit);
    }

    [Fact]
    public void ShouldMarkStrongMatch_WhenPlaceDoesNotMatch() {
        AdView view = MatchingFixtures.Ad with { Location = "Oslo" };

        DigestCard card = RunConversions.ToDigestCard(view, adUrl, MatchingFixtures.AllRoles() with { Places = ["Bergen"] });

        Assert.Equal((DigestFit.Partial, true), (card.Fit, card.IsStrongMatch));
    }

    [Fact]
    public void ShouldMarkApplyExpired_WhenApplyStatusIsExpired() {
        AdView view = MatchingFixtures.Ad with { ApplyStatus = "expired" };

        DigestCard card = RunConversions.ToDigestCard(view, adUrl, MatchingFixtures.AllRoles());

        Assert.True(card.ApplyExpired);
    }

    [Fact]
    public void ShouldParseDeadline_WhenAdHasDeadline() {
        AdView view = MatchingFixtures.Ad with { Deadline = "2026-10-12" };

        DigestCard card = RunConversions.ToDigestCard(view, adUrl, MatchingFixtures.AllRoles());

        Assert.Equal(new DateOnly(2026, 10, 12), card.DeadlineDate);
    }

    [Theory]
    [InlineData("Senior")]
    [InlineData("Years")]
    [InlineData("Masters")]
    [InlineData("Role")]
    [InlineData("Place")]
    [InlineData("Other")]
    public void ShouldMapHiddenKind_WhenCountsAreConverted(string name) {
        Dictionary<HiddenKind, int> counts = new() { [Enum.Parse<HiddenKind>(name)] = hiddenCount };

        IReadOnlyList<HiddenCount> hidden = RunConversions.ToHiddenCounts(counts);

        Assert.Equal([new HiddenCount(Enum.Parse<HiddenGroup>(name), hiddenCount)], hidden);
    }
}
