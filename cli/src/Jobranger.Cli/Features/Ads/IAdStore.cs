using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Ads;

internal interface IAdStore {
    bool Upsert(Ad ad);

    IReadOnlyList<Ad> PendingDescription(IEnumerable<string> skipLabels);

    void BumpDescriptionAttempts(string adId);

    void SetDescription(string adId, string text, string? applyUrl);

    void SetApplyStatus(string adId, string status, DateTimeOffset checkedAt);

    IReadOnlyList<Ad> PendingApplyUrl(string source, int limit, Targets targets);

    IReadOnlyList<Ad> PendingApplyCheck(DateOnly asOf, DateTimeOffset recheckBefore, int limit, Targets targets);

    IReadOnlyList<Ad> PendingClassification(int maxAttempts);

    IReadOnlyList<Ad> ExhaustedClassification(int maxAttempts, string version);

    IReadOnlyList<Ad> OutdatedClassification(string version);

    void BumpLabelAttempts(IReadOnlyList<string> adIds);

    void SetClassification(string adId, StoredClassification classification, string version);

    void SetLabel(string adId, string label, string version);

    IReadOnlySet<string> KnownIds();

    IReadOnlyList<Ad> NewSince(DateTimeOffset since);

    IReadOnlyList<Ad> Reportable(DateOnly asOf, Func<Ad, bool> isReportable);

    IReadOnlyList<Ad> ReportedSince(DateTimeOffset since);

    void MarkReported(IReadOnlyList<string> adIds, DateTimeOffset at);

    IReadOnlyList<Ad> Active();

    IReadOnlyList<Ad> ById(IEnumerable<string> adIds);

    void ResetUnassessed(string version);

    long CountOutdated(string version);

    void MarkAd(string adId, AdAction action, DateTimeOffset at, string? cvName = null);

    void UnmarkAd(string adId, AdAction action, DateTimeOffset at);

    IReadOnlyDictionary<string, IReadOnlySet<AdAction>> Actions();

    IReadOnlySet<string> ShownAnyway();

    IReadOnlyList<Ad> KeepReportable(IReadOnlyList<Ad> ads, Func<Ad, bool> isReportable);

    void SetActive(string adId, bool active);

    void SyncSourceActive(string source, IEnumerable<string> activeIds);

    void MarkCategoryUnlisted(IReadOnlyList<string> adIds);

    void SyncFinnActive(
        IReadOnlyCollection<string> categoryIds, IReadOnlyCollection<string> recentIds, DateTimeOffset? windowStart);

    int ClosePastDeadline(DateOnly asOf);

    IReadOnlyList<ClosedAd> Closed(DateOnly asOf, int keepDays = AdStore.ClosedKeepDays);

    long ClosedSince(string since);
}
