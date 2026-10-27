using Jobranger.Cli.Config;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed class Ads(SqliteConnection connection, TimeProvider time) {
    private const int applyCheckLimit = 60;

    private static readonly TimeSpan applyRecheckAge = TimeSpan.FromDays(7);

    private readonly AdStore store = new(connection, time);

    public bool Upsert(Ad ad) => store.Upsert(ad);

    public IReadOnlyList<Ad> Pending(int maxAttempts) => store.PendingClassification(maxAttempts);

    public void SetClassification(string adId, StoredClassification classification, string version) =>
        store.SetClassification(adId, classification, version);

    public void SetLabel(string adId, string label, string version) => store.SetLabel(adId, label, version);

    public long CountOutdated(string version) => store.CountOutdated(version);

    public IReadOnlyList<Ad> Reportable(DateOnly today, Func<Ad, bool> isReportable) =>
        store.Reportable(today, isReportable);

    public IReadOnlyList<Ad> KeepReportable(IReadOnlyList<Ad> ads, Func<Ad, bool> isReportable) =>
        store.KeepReportable(ads, isReportable);

    public void MarkReported(IReadOnlyList<string> adIds, DateTimeOffset at) => store.MarkReported(adIds, at);

    public async Task CheckApplyLinksAsync(
        HttpClient client, DateTimeOffset now, Targets targets, CancellationToken cancellationToken) {
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        foreach (Ad ad in store.PendingApplyCheck(today, now - applyRecheckAge, applyCheckLimit, targets)) {
            if (ad.ApplyUrl is string url) {
                ApplyStatus status = await CheckAsync(client, url, cancellationToken).ConfigureAwait(false);
                store.SetApplyStatus(ad.Id, WireNames.Of(status), now);
            }
        }
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
