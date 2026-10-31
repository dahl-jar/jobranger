using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Learning;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Features.Learning.DecisionFixtures;

namespace Jobranger.Cli.Tests.Features.Learning;

[UnsupportedOSPlatform("windows")]
public sealed class DecisionStoreTests : IDisposable {
    private const int k = 5;
    private const int singleAttempt = 1;
    private const string movedText = "Backend Developer. Build Go services in Oslo.";

    private static readonly float[] vector = [1f, 0f];

    private readonly TestDatabase db = new();
    private readonly DecisionStore store;
    private readonly VectorIndex index;
    private readonly AdStore ads;

    public DecisionStoreTests() {
        store = new(db.Connection);
        index = new(db.Connection);
        ads = new(db.Connection, AdFixtures.Clock());
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldReadBackDecision_WhenRecorded() {
        long id = store.Record(Primary);

        Decision read = Assert.Single(store.List());
        Assert.Equal(
            (id, "finn:1", "org one as|backend developer", DecisionAction.Applied, "2026-10-09T10:00:00+00:00",
                "Backend Developer", "Org One AS", "Build Go services.", "Go|Bachelor's degree",
                "Backend Developer. Build Go services in Bergen.", (string?)null),
            (read.Id, read.AdId, read.DedupKey, read.Action, read.DecidedAt, read.Title, read.Company, read.Summary,
                string.Join('|', read.Requirements), read.EmbedText, read.EmbeddedModel));
    }

    [Theory]
    [InlineData(nameof(DecisionAction.Applied), "applied")]
    [InlineData(nameof(DecisionAction.NotForMe), "not_for_me")]
    [InlineData(nameof(DecisionAction.ShowAnyway), "show_anyway")]
    public void ShouldStoreActionName_WhenRecorded(string actionName, string name) {
        DecisionAction action = Enum.Parse<DecisionAction>(actionName);

        store.Record(Primary with { Action = action });

        Assert.Equal((name, action), (db.Scalar("SELECT action FROM decisions"), Assert.Single(store.List()).Action));
    }

    [Fact]
    public void ShouldReplaceDecisionInPlace_WhenAdIsDecidedAgain() {
        long first = store.Record(Primary);

        long second = store.Record(Primary with {
            DedupKey = "org one as|go developer",
            Action = DecisionAction.NotForMe,
            DecidedAt = DecidedAt.AddDays(1),
            Title = "Go Developer",
            Company = "Org Two AS",
            Summary = "Run Go services.",
            Requirements = ["Kubernetes"],
            EmbedText = movedText,
        });

        Decision read = Assert.Single(store.List());
        Assert.Equal(
            (first, first, "finn:1", "org one as|go developer", DecisionAction.NotForMe, "2026-10-10T10:00:00+00:00",
                "Go Developer", "Org Two AS", "Run Go services.", "Kubernetes", movedText),
            (second, read.Id, read.AdId, read.DedupKey, read.Action, read.DecidedAt, read.Title, read.Company,
                read.Summary, string.Join('|', read.Requirements), read.EmbedText));
    }

    [Fact]
    public void ShouldEmbedAgain_WhenAdIsDecidedAgain() {
        long id = store.Record(Primary);
        store.StoreEmbedding(id, Primary.EmbedText, CurrentModel, vector);

        store.Record(Primary with { EmbedText = movedText });

        Assert.Equal([id], store.PendingEmbeddings(CurrentModel).Select(decision => decision.Id));
    }

    [Fact]
    public void ShouldStoreVector_WhenEmbeddedTextIsCurrent() {
        long id = store.Record(Primary);

        bool stored = store.StoreEmbedding(id, Primary.EmbedText, CurrentModel, vector);

        Assert.Equal((true, CurrentModel, id),
            (stored, Assert.Single(store.List()).EmbeddedModel, Assert.Single(index.Nearest(vector, k)).DecisionId));
    }

    [Fact]
    public void ShouldSkipEmbedding_WhenAdIsDecidedAgainMeanwhile() {
        long id = store.Record(Primary);
        Decision pending = Assert.Single(store.PendingEmbeddings(CurrentModel));
        store.Record(Primary with { EmbedText = movedText });

        bool stored = store.StoreEmbedding(id, pending.EmbedText, CurrentModel, vector);

        Assert.Equal((false, (string?)null, 0),
            (stored, Assert.Single(store.List()).EmbeddedModel, index.Nearest(vector, k).Count));
    }

    [Fact]
    public void ShouldDropOldVector_WhenAdIsDecidedAgain() {
        index.Add(store.Record(Primary), vector);

        store.Record(Primary with { Action = DecisionAction.NotForMe });

        Assert.Empty(index.Nearest(vector, k));
    }

    [Fact]
    public void ShouldListNewestFirst_WhenDecisionsHaveDifferentTimes() {
        long older = store.Record(Numbered(1));
        long newer = store.Record(Numbered(2) with { DecidedAt = DecidedAt.AddDays(1) });
        long oldest = store.Record(Numbered(3) with { DecidedAt = DecidedAt.AddDays(-1) });

        Assert.Equal([newer, older, oldest], store.List().Select(decision => decision.Id));
    }

    [Fact]
    public void ShouldDropVector_WhenDecisionIsWithdrawn() {
        long id = store.Record(Primary);
        index.Add(id, vector);

        long? withdrawn = store.Withdraw(Primary.AdId);

        Assert.Equal((id, 0), (withdrawn, index.Nearest(vector, k).Count));
    }

    [Fact]
    public void ShouldReturnNull_WhenWithdrawingUnknownAd() {
        store.Record(Primary);

        Assert.Null(store.Withdraw("finn:unknown"));
    }

    [Fact]
    public void ShouldListUnembeddedAndOtherModel_WhenAskingPendingEmbeddings() {
        long unembedded = store.Record(Numbered(1));
        long current = store.Record(Numbered(2));
        long old = store.Record(Numbered(3));
        store.StoreEmbedding(current, Primary.EmbedText, CurrentModel, vector);
        store.StoreEmbedding(old, Primary.EmbedText, OldModel, vector);

        IReadOnlyList<Decision> pending = store.PendingEmbeddings(CurrentModel);

        Assert.Equal([unembedded, old], pending.Select(decision => decision.Id));
    }

    [Fact]
    public void ShouldReturnOnlyRequestedDecisions_WhenReadingById() {
        long first = store.Record(Numbered(1));
        store.Record(Numbered(2));
        long third = store.Record(Numbered(3));

        IReadOnlyDictionary<long, Decision> found = store.ById([first, third]);

        Assert.Equal([(first, "finn:1"), (third, "finn:3")],
            found.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.AdId)));
    }

    [Fact]
    public void ShouldCountExampleUses_WhenAdIsClassifiedAgain() {
        Ad checkedAd = StoreAd(1);
        Ad otherAd = StoreAd(2);
        long first = store.Record(Numbered(1));
        long second = store.Record(Numbered(2));
        store.RecordExamples(checkedAd.Id, [first, second]);
        store.RecordExamples(otherAd.Id, [second]);

        store.RecordExamples(checkedAd.Id, [second]);

        Assert.Equal([KeyValuePair.Create(second, 2)], store.UseCounts());
    }

    [Fact]
    public void ShouldRequeueAd_WhenExampleWasForgottenDuringClassification() {
        Ad ad = StoreAd(1);
        long forgotten = store.Record(Numbered(1));
        long kept = store.Record(Numbered(2));
        store.Forget(forgotten);

        bool requeued = store.RecordExamples(ad.Id, [forgotten, kept]);

        Assert.Equal((true, ad.Id, 0),
            (requeued, string.Join('|', ads.PendingClassification(singleAttempt).Select(pending => pending.Id)),
                store.UseCounts().Count));
    }

    [Fact]
    public void ShouldRequeueOnlyActiveUnactedAds_WhenDecisionIsForgotten() {
        Ad unacted = StoreAd(1);
        Ad acted = StoreAd(2);
        Ad inactive = StoreAd(3);
        ads.MarkAd(acted.Id, AdAction.Applied, AdFixtures.Now);
        ads.SetActive(inactive.Id, active: false);
        long id = store.Record(Primary);
        foreach (Ad ad in new[] { unacted, acted, inactive }) {
            store.RecordExamples(ad.Id, [id]);
        }

        IReadOnlyList<string>? requeued = store.Forget(id);

        Assert.Equal([unacted.Id], requeued);
    }

    [Fact]
    public void ShouldQueueForClassification_WhenAdIsRequeued() {
        Ad ad = StoreAd(1);
        ads.BumpLabelAttempts([ad.Id]);
        long id = store.Record(Primary);
        store.RecordExamples(ad.Id, [id]);

        store.Forget(id);

        Assert.Equal([ad.Id], ads.PendingClassification(singleAttempt).Select(pending => pending.Id));
    }

    [Fact]
    public void ShouldClearAssessment_WhenAdIsRequeued() {
        Ad ad = StoreAd(1);
        long id = store.Record(Primary);
        store.RecordExamples(ad.Id, [id]);

        store.Forget(id);

        Assert.Equal(["NULL|NULL|NULL"],
            TestDatabase.Rows(db.Connection, "SELECT assessment, summary, technologies FROM ads"));
    }

    [Fact]
    public void ShouldStopCountingUses_WhenAdIsRequeued() {
        Ad ad = StoreAd(1);
        long forgotten = store.Record(Numbered(1));
        long kept = store.Record(Numbered(2));
        store.RecordExamples(ad.Id, [forgotten, kept]);

        store.Forget(forgotten);

        Assert.Empty(store.UseCounts());
    }

    [Fact]
    public void ShouldStopCountingUses_WhenForgottenDecisionWasUsedByActedAd() {
        Ad acted = StoreAd(1);
        ads.MarkAd(acted.Id, AdAction.Applied, AdFixtures.Now);
        long id = store.Record(Primary);
        store.RecordExamples(acted.Id, [id]);

        store.Forget(id);

        Assert.Empty(store.UseCounts());
    }

    [Fact]
    public void ShouldDeleteDecision_WhenForgotten() {
        long id = store.Record(Primary);

        store.Forget(id);

        Assert.Empty(store.List());
    }

    [Fact]
    public void ShouldReturnNull_WhenForgettingUnknownDecision() {
        long id = store.Record(Primary);

        Assert.Null(store.Forget(id + 1));
    }

    [Fact]
    public void ShouldNotReuseId_WhenNewestDecisionIsForgotten() {
        long forgotten = store.Record(Primary);
        store.Forget(forgotten);

        long next = store.Record(Numbered(2));

        Assert.NotEqual(forgotten, next);
    }

    [Fact]
    public void ShouldRemoveEveryDecision_WhenForgettingAll() {
        Ad ad = StoreAd(1);
        long first = store.Record(Numbered(1));
        store.Record(Numbered(2));
        store.RecordExamples(ad.Id, [first]);

        IReadOnlyList<string> requeued = store.ForgetAll();

        Assert.Equal((ad.Id, 0L), (string.Join('|', requeued), store.Count()));
    }

    private Ad StoreAd(int number) {
        Ad ad = AdFixtures.Numbered(number) with { Description = "Build Go services." };
        ads.Upsert(ad);
        ads.SetClassification(ad.Id, AdFixtures.Classification, AdFixtures.CurrentVersion);
        return ad;
    }
}
