using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class DigestBuildTests {
    private static readonly DateOnly today = DigestFixtures.Today;
    private static readonly DigestInput input = DigestFixtures.Input;

    [Theory]
    [InlineData("2026-10-15", true)]
    [InlineData("2026-10-16", false)]
    public void ShouldPutCardInApplySoon_WhenDeadlineIsWithinSevenDays(string deadline, bool soon) {
        DigestData data = DigestEntry.Build(input with { Cards = [DigestFixtures.Due("finn:1", deadline)] }, today);

        Assert.Equal((soon, !soon), (data.ApplySoon.Count == 1, data.NewToday.Count == 1));
    }

    [Fact]
    public void ShouldListCarriedCard_WhenItIsDueSoon() {
        DigestInput carried = input with { Cards = [DigestFixtures.Due("finn:1", "2026-10-10")], CarriedIds = ["finn:1"] };

        DigestData data = DigestEntry.Build(carried, today);

        Assert.Equal(["finn:1"], Ids(data.Carried));
        Assert.Empty(data.ApplySoon);
    }

    [Fact]
    public void ShouldPutStrongMatchFirst_WhenNewTodayIsSorted() {
        DigestCard partial = DigestFixtures.Due("finn:1", "2026-10-16") with { IsStrongMatch = false };
        DigestCard strong = DigestFixtures.Due("finn:2", "2026-10-30");

        DigestData data = DigestEntry.Build(input with { Cards = [partial, strong] }, today);

        Assert.Equal(["finn:2", "finn:1"], Ids(data.NewToday));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldSortByDeadlineWithUndatedLast_WhenGroupIsBuilt(bool carried) {
        DigestInput unsorted = input with {
            Cards = [
                DigestFixtures.Due("finn:1", null),
                DigestFixtures.Due("finn:2", "2026-10-30"),
                DigestFixtures.Due("finn:3", "2026-10-20"),
            ],
            CarriedIds = carried ? ["finn:1", "finn:2", "finn:3"] : [],
        };

        DigestData data = DigestEntry.Build(unsorted, today);

        Assert.Equal(["finn:3", "finn:2", "finn:1"], Ids(carried ? data.Carried : data.NewToday));
    }

    [Theory]
    [InlineData("2026-10-07", false)]
    [InlineData("2026-10-20", true)]
    public void ShouldDropCard_WhenAdHasExpired(string deadline, bool applyExpired) {
        DigestCard card = DigestFixtures.Due("finn:1", deadline) with { ApplyExpired = applyExpired };

        DigestData data = DigestEntry.Build(input with { Cards = [card] }, today);

        Assert.Empty(data.ApplySoon.Concat(data.NewToday).Concat(data.Carried));
    }

    [Fact]
    public void ShouldKeepCard_WhenDeadlineIsToday() {
        DigestData data = DigestEntry.Build(input with { Cards = [DigestFixtures.Due("finn:1", "2026-10-08")] }, today);

        Assert.Equal(["finn:1"], Ids(data.ApplySoon));
    }

    [Fact]
    public void ShouldCountEmployerPages_WhenCareerPagesWereRead() {
        DigestInput read = input with { SourcesRead = ["finn", "teamtailor:acme", "ashby:beta", "ashbyx"] };

        Assert.Equal(2, DigestEntry.Build(read, today).EmployerPages);
    }

    [Fact]
    public void ShouldDropModelStop_WhenNoAdIsPending() {
        DigestInput stopped = input with { ModelStopped = DigestStop.Auth, Pending = 0 };

        Assert.Null(DigestEntry.Build(stopped, today).ModelStopped);
    }

    [Fact]
    public void ShouldKeepModelStop_WhenAdsArePending() {
        DigestInput stopped = input with { ModelStopped = DigestStop.ProviderDown, Pending = 5 };

        Assert.Equal(DigestStop.ProviderDown, DigestEntry.Build(stopped, today).ModelStopped);
    }

    [Fact]
    public void ShouldCopyRunFacts_WhenDigestIsBuilt() {
        HiddenCount[] hidden = [new(HiddenGroup.Senior, 4)];
        string[] failed = ["nav"];
        DigestInput run = input with {
            Hidden = hidden,
            FailedSources = failed,
            ClosedSinceLast = 3,
            BudgetPaused = true,
            Pending = 2,
            MaxYears = 2.5,
        };

        DigestData data = DigestEntry.Build(run, today);

        Assert.Equal(
            (today, DigestFixtures.RanAt, 12, 3, true, 2, 2.5),
            (data.Day, data.RanAt, data.NewAds, data.ClosedSinceLast, data.BudgetPaused, data.Pending, data.MaxYears));
        Assert.Equal(hidden, data.Hidden);
        Assert.Equal(failed, data.FailedSources);
        Assert.Equal(input.SourcesRead, data.SourcesRead);
    }

    private static string[] Ids(IReadOnlyList<DigestCard> cards) => [.. cards.Select(card => card.Id)];
}
