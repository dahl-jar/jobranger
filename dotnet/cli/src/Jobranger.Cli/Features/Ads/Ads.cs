using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed class Ads(SqliteConnection connection, TimeProvider time) {
    public const string ErrorLabel = AdStore.ErrorLabel;

    private const int applyCheckLimit = 60;

    private static readonly TimeSpan applyRecheckAge = TimeSpan.FromDays(7);

    private readonly AdStore store = new(connection, time);
    private readonly StateStore state = new(connection);

    public static IReadOnlyList<IReadOnlyList<Ad>> GroupDuplicates(IReadOnlyList<Ad> ads) => Duplicates.Group(ads);

    public static (IReadOnlyList<Ad> Remaining, IReadOnlyList<string> Covered) SplitReported(
        IReadOnlyList<Ad> ads, IReadOnlyList<Ad> reported) => Duplicates.SplitReported(ads, reported);

    public static bool CompaniesMatch(string left, string right) => Duplicates.CompaniesMatch(left, right);

    public static bool SameTitle(string left, string right) => Duplicates.SameTitle(left, right);

    public bool Upsert(Ad ad) => store.Upsert(ad);

    public IReadOnlySet<string> KnownIds() => store.KnownIds();

    public IReadOnlyDictionary<string, string> Cursors() => state.Cursors();

    public void SetCursor(string key, string value) => state.Set(key, value);

    public IReadOnlyList<Ad> PendingDescription(IEnumerable<string> skipLabels) => store.PendingDescription(skipLabels);

    public IReadOnlyList<Ad> PendingApplyUrl(string source, int limit, Targets targets) =>
        store.PendingApplyUrl(source, limit, targets);

    public void SetActive(string adId, bool active) => store.SetActive(adId, active);

    public void MarkCategoryUnlisted(IReadOnlyList<string> adIds) => store.MarkCategoryUnlisted(adIds);

    public void SyncFinnActive(
        IReadOnlyCollection<string> categoryIds, IReadOnlyCollection<string> recentIds, DateTimeOffset? windowStart) =>
        store.SyncFinnActive(categoryIds, recentIds, windowStart);

    public void SyncSourceActive(string source, IEnumerable<string> activeIds) => store.SyncSourceActive(source, activeIds);

    public void BumpDescriptionAttempts(string adId) => store.BumpDescriptionAttempts(adId);

    public void SetDescription(string adId, string text, string? applyUrl) => store.SetDescription(adId, text, applyUrl);

    public void SetApplyStatus(string adId, ApplyStatus status, DateTimeOffset checkedAt) =>
        store.SetApplyStatus(adId, WireNames.Of(status), checkedAt);

    public IReadOnlyList<Ad> Pending(int maxAttempts) => store.PendingClassification(maxAttempts);

    public IReadOnlyList<Ad> Exhausted(int maxAttempts, string version) =>
        store.ExhaustedClassification(maxAttempts, version);

    public IReadOnlyList<Ad> Outdated(string version) => store.OutdatedClassification(version);

    public void BumpLabelAttempts(IReadOnlyList<string> adIds) => store.BumpLabelAttempts(adIds);

    public void ResetUnassessed(string version) => store.ResetUnassessed(version);

    public void SetClassification(string adId, StoredClassification classification, string version) =>
        store.SetClassification(adId, classification, version);

    public void SetLabel(string adId, string label, string version) => store.SetLabel(adId, label, version);

    public long CountOutdated(string version) => store.CountOutdated(version);

    public IReadOnlyList<Ad> Active() => store.Active();

    public int ClosePastDeadline(DateOnly today) => store.ClosePastDeadline(today);

    public IReadOnlyList<Ad> Reportable(DateOnly today, Func<Ad, bool> isReportable) =>
        store.Reportable(today, isReportable);

    public IReadOnlyList<Ad> KeepReportable(IReadOnlyList<Ad> ads, Func<Ad, bool> isReportable) =>
        store.KeepReportable(ads, isReportable);

    public IReadOnlyList<Ad> ReportedSince(DateTimeOffset since) => store.ReportedSince(since);

    public IReadOnlySet<string> ShownAnyway() => store.ShownAnyway();

    public IReadOnlyList<Ad> NewSince(DateTimeOffset since) => store.NewSince(since);

    public void MarkReported(IReadOnlyList<string> adIds, DateTimeOffset at) => store.MarkReported(adIds, at);

    public async Task<IReadOnlyDictionary<ApplyStatus, int>> CheckApplyLinksAsync(
        HttpClient client, DateTimeOffset now, DateOnly today, Targets targets, CancellationToken cancellationToken) {
        Dictionary<ApplyStatus, int> counts = [];
        foreach (Ad ad in store.PendingApplyCheck(today, now - applyRecheckAge, applyCheckLimit, targets)) {
            if (ad.ApplyUrl is string url) {
                ApplyStatus status = await CheckAsync(client, url, cancellationToken).ConfigureAwait(false);
                SetApplyStatus(ad.Id, status, now);
                counts[status] = counts.GetValueOrDefault(status) + 1;
            }
        }
        return counts;
    }

    private async Task<ApplyStatus> CheckAsync(HttpClient client, string url, CancellationToken cancellationToken) {
        try {
            return await ApplyLinks.CheckAsync(client, url, time, cancellationToken).ConfigureAwait(false);
        } catch (Exception error) when (error is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested) {
            return ApplyStatus.Unknown;
        }
    }
}
