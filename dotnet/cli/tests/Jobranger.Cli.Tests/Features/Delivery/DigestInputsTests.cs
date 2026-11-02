using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Tests.Features.Delivery;

[UnsupportedOSPlatform("windows")]
public sealed class DigestInputsTests : IDisposable {
    private const string twinTitle = "Developer";
    private const string twinCompany = "Org One AS";
    private const string longDescription = "A long description of the same job, longer than the one on the twin ad.";
    private const int runLimit = 2;

    private static readonly AppConfig config = DeliveryFixtures.Config(DeliveryFixtures.ResendEmail);

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider time = AdFixtures.Clock();
    private readonly AdsEntry ads;
    private readonly AdStore store;

    public DigestInputsTests() {
        ads = new AdsEntry(db.Connection, time);
        store = new AdStore(db.Connection, time);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldKeepRawTitle_WhenCardIsBuilt() {
        const string raw = "<b>Dev</b> *x*";
        Ad ad = DeliveryFixtures.Stored(
            ads, RunFixtures.Pending(1, twinTitle, twinCompany), DeliveryFixtures.Classification with { DisplayTitle = raw });

        DigestInput input = Input(DeliveryFixtures.Result(ad));

        Assert.Equal(raw, Assert.Single(input.Cards).DisplayTitle);
    }

    [Fact]
    public void ShouldCountSameAdOnce_WhenTwoSourcesHaveIt() {
        Ad finn = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, "Junior utvikler", twinCompany));
        Ad nav = DeliveryFixtures.Stored(ads, RunFixtures.Pending(2, "Junior Utvikler", "org one") with {
            Id = "nav:aa-bb",
            Source = "nav",
            SourceId = "aa-bb",
        });

        DigestInput input = Input(DeliveryFixtures.Result(finn, nav));

        Assert.Single(input.Cards);
    }

    [Fact]
    public void ShouldPreferDirectEmployerUrl_WhenAdIsDuplicate() {
        Ad nav = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, twinTitle, twinCompany) with {
            Id = "nav:1",
            Source = "nav",
            Url = "https://jobs.example.test/stilling/1",
        });
        Ad direct = DeliveryFixtures.Stored(ads, RunFixtures.Pending(2, twinTitle, twinCompany) with {
            Id = "teamtailor:example:1",
            Source = "teamtailor:example",
            Url = "https://careers.example.test/jobs/1",
        });

        DigestInput input = Input(DeliveryFixtures.Result(nav, direct));

        Assert.Equal(direct.Url, Assert.Single(input.Cards).Url);
    }

    [Fact]
    public void ShouldCountNoClosedAds_WhenNoPreviousRun() {
        Ad ad = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, twinTitle, twinCompany));
        long run = new RunStore(db.Connection).Start(time.GetUtcNow());
        time.Advance(TimeSpan.FromHours(1));
        ads.SetActive(ad.Id, false);

        DigestInput input = Input(DeliveryFixtures.Result() with { RunId = run });

        Assert.Equal(0, input.ClosedSinceLast);
    }

    [Fact]
    public void ShouldCountClosedAds_WhenPreviousRunExists() {
        Ad ad = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, twinTitle, twinCompany));
        RunStore runs = new(db.Connection);
        runs.Start(time.GetUtcNow());
        time.Advance(TimeSpan.FromHours(1));
        ads.SetActive(ad.Id, false);
        time.Advance(TimeSpan.FromHours(1));
        long current = runs.Start(time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result() with { RunId = current });

        Assert.Equal(1, input.ClosedSinceLast);
    }

    [Fact]
    public void ShouldUseCurrentMatch_WhenDuplicateHasOlderOne() {
        Ad stale = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(1, twinTitle, twinCompany) with { Id = "nav:1", Source = "nav", Description = longDescription },
            OutdatedMatch(Fit.Unsuitable));
        Ad current = DeliveryFixtures.Stored(ads, RunFixtures.Pending(2, twinTitle, twinCompany));

        DigestInput input = Input(DeliveryFixtures.Result(stale, current));

        Assert.Equal(current.Id, Assert.Single(input.Cards).Id);
    }

    [Fact]
    public void ShouldKeepShownAnywayAd_WhenRulesWouldHideIt() {
        Ad ad = DeliveryFixtures.Stored(
            ads, RunFixtures.Pending(1, twinTitle, twinCompany), DeliveryFixtures.Classification with { IsProgrammingJob = false });
        store.MarkAd(ad.Id, AdAction.ShowAnyway, time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result(ad));

        Assert.Equal(ad.Id, Assert.Single(input.Cards).Id);
    }

    [Fact]
    public void ShouldTakeMatchFromCurrentTwin_WhenOtherIsShownAnyway() {
        Ad outdated = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(1, twinTitle, twinCompany) with { Id = "nav:1", Source = "nav", Description = longDescription },
            OutdatedMatch(Fit.Stretch));
        Ad current = DeliveryFixtures.Stored(ads, RunFixtures.Pending(2, twinTitle, twinCompany));
        store.MarkAd(outdated.Id, AdAction.ShowAnyway, time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result(outdated, current));

        DigestCard card = Assert.Single(input.Cards);
        Assert.Equal(DigestFit.Strong, card.Fit);
        Assert.True(card.IsStrongMatch);
    }

    [Fact]
    public void ShouldNotCountCardAsStrong_WhenMatchIsStretch() {
        Ad ad = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(1, twinTitle, twinCompany),
            DeliveryFixtures.Classification with {
                Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch with { Status = Fit.Stretch }, []),
            });

        DigestInput input = Input(DeliveryFixtures.Result(ad));

        Assert.False(Assert.Single(input.Cards).IsStrongMatch);
    }

    [Theory]
    [InlineData(Fit.Stretch, nameof(DigestFit.Partial))]
    [InlineData(Fit.Unsuitable, nameof(DigestFit.OnRequest))]
    public void ShouldLabelFit_WhenMatchIsNotStrong(string status, string expected) {
        Ad ad = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(1, twinTitle, twinCompany),
            DeliveryFixtures.Classification with {
                Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch with { Status = status }, []),
            });
        store.MarkAd(ad.Id, AdAction.ShowAnyway, time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result(ad));

        Assert.Equal(Enum.Parse<DigestFit>(expected), Assert.Single(input.Cards).Fit);
    }

    [Fact]
    public void ShouldShowMergedFields_WhenTwinsDiffer() {
        Ad selected = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(1, "Junior utvikler", twinCompany) with {
                Id = "nav:1",
                Source = "nav",
                Location = "Bergen sentrum",
                Url = "https://jobs.example.test/stilling/1",
                Description = longDescription,
                Deadline = "2026-11-20",
            },
            OutdatedMatch(Fit.Strong) with {
                DisplayTitle = "Outdated title",
                Summary = "Outdated summary",
                RoleType = "frontend",
            });
        Ad current = DeliveryFixtures.Stored(
            ads,
            RunFixtures.Pending(2, "Junior Utvikler", "org one") with { Deadline = "2026-11-10" },
            DeliveryFixtures.Classification with { DisplayTitle = "Current title", Summary = "Current summary" });
        store.MarkAd(selected.Id, AdAction.ShowAnyway, time.GetUtcNow());
        ads.SetApplyStatus(current.Id, ApplyStatus.Expired, time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result(selected, current));

        DigestCard card = Assert.Single(input.Cards);
        Assert.Equal(selected.Id, card.Id);
        Assert.Equal("nav", card.Source);
        Assert.Equal("Junior utvikler", card.Title);
        Assert.Equal(twinCompany, card.Company);
        Assert.Equal("Bergen sentrum", card.Location);
        Assert.Equal(selected.Url, card.Url);
        Assert.Equal("Current title", card.DisplayTitle);
        Assert.Equal("Current summary", card.Summary);
        Assert.Equal("Backend", card.RoleLabel);
        Assert.Equal("2026-11-10", card.Deadline);
        Assert.Equal(new DateOnly(2026, 11, 10), card.DeadlineDate);
        Assert.True(card.ApplyExpired);
    }

    [Theory]
    [InlineData(nameof(StopReason.Auth), nameof(DigestStop.Auth))]
    [InlineData(nameof(StopReason.ProviderDown), nameof(DigestStop.ProviderDown))]
    [InlineData(nameof(StopReason.Budget), null)]
    [InlineData(nameof(StopReason.Cancelled), null)]
    public void ShouldMapStopReason_WhenPassStopped(string stop, string? expected) {
        RunResult result = DeliveryFixtures.Result() with { ModelStopped = Enum.Parse<StopReason>(stop) };

        DigestInput input = Input(result);

        Assert.Equal(expected, input.ModelStopped?.ToString());
    }

    [Theory]
    [InlineData(nameof(HiddenKind.Senior))]
    [InlineData(nameof(HiddenKind.Years))]
    [InlineData(nameof(HiddenKind.Masters))]
    [InlineData(nameof(HiddenKind.Role))]
    [InlineData(nameof(HiddenKind.Place))]
    [InlineData(nameof(HiddenKind.Other))]
    public void ShouldMapHiddenKind_WhenPassHidAds(string kind) {
        RunResult result = DeliveryFixtures.Result() with {
            HiddenCounts = new Dictionary<HiddenKind, int> { [Enum.Parse<HiddenKind>(kind)] = 3 },
        };

        DigestInput input = Input(result);

        Assert.Equal(new HiddenCount(Enum.Parse<HiddenGroup>(kind), 3), Assert.Single(input.Hidden));
    }

    [Fact]
    public void ShouldBuildNoCards_WhenPassHasNoVersion() {
        Ad ad = DeliveryFixtures.Stored(ads, RunFixtures.Pending(1, twinTitle, twinCompany));
        store.MarkAd(ad.Id, AdAction.ShowAnyway, time.GetUtcNow());

        DigestInput input = Input(DeliveryFixtures.Result(ad) with { Version = null });

        Assert.Empty(input.Cards);
    }

    [Fact]
    public void ShouldCopyRunFactsIntoDigest() {
        RunResult result = DeliveryFixtures.Result() with {
            CarriedIds = ["finn:9"],
            NewAds = 5,
            SourcesRead = ["finn"],
            FailedSources = ["nav"],
            ModelStopped = StopReason.Budget,
            Pending = 4,
        };

        DigestInput input = Input(result);

        Assert.Equal(["finn:9"], input.CarriedIds);
        Assert.Equal(5, input.NewAds);
        Assert.Equal(["finn"], input.SourcesRead);
        Assert.Equal(["nav"], input.FailedSources);
        Assert.True(input.BudgetPaused);
        Assert.Equal(4, input.Pending);
        Assert.Equal(AdFixtures.Now, input.RanAt);
        Assert.Equal(MatchingEntry.MaxYears(config.Targets.Limits), input.MaxYears);
    }

    private static StoredClassification OutdatedMatch(string status) => DeliveryFixtures.Classification with {
        Assessment = MatchingEntry.EncodeMatch(
            MatchingFixtures.StrongMatch with { Version = MatchingFixtures.OutdatedVersion, Status = status }, []),
    };

    private DigestInput Input(RunResult result) =>
        DigestInputs.From(ads, RunsEntry.Recent(db.Connection, runLimit), result, config, time.GetUtcNow());
}
