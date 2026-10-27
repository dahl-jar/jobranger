using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed class Ads(SqliteConnection connection, TimeProvider time) {
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
}
