using System.Globalization;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Sources;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Features.Runs;

internal sealed class AdsBatchWriter(AdsEntry ads, string version, DateTimeOffset runStarted) : IBatchWriter {
    private readonly Dictionary<string, string> jobTitles = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> JobTitles => jobTitles;

    public int Write(SourceBatch batch) {
        int created = batch.Listings.Count(listing => ads.Upsert(RunConversions.ToAd(listing)));
        foreach ((string adId, bool active) in batch.Active) {
            ads.SetActive(adId, active);
        }
        if (batch.FinnSync is FinnSync sync) {
            ads.MarkCategoryUnlisted(sync.UnlistedIds);
            if (sync.Complete && !batch.Partial) {
                ads.SyncFinnActive(sync.CategoryIds, sync.RecentIds, WindowStart(sync.WindowStart));
            }
        }
        foreach (PageResult page in batch.Pages) {
            WritePage(page);
        }
        if (batch.ListedIds is IReadOnlySet<string> listed && !batch.Partial) {
            ads.SyncSourceActive(batch.Source, listed);
        }
        foreach ((string key, string value) in batch.Cursors) {
            ads.SetCursor(key, value);
        }
        foreach ((string adId, string jobTitle) in batch.JobTitles) {
            jobTitles[adId] = jobTitle;
        }
        return created;
    }

    private static DateTimeOffset? WindowStart(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset start)
            ? start
            : null;

    private void WritePage(PageResult page) {
        if (page.Label is string label) {
            ads.SetLabel(page.ListingId, label, version);
        } else if (page.Description is not string description) {
            ads.BumpDescriptionAttempts(page.ListingId);
        } else {
            ads.SetDescription(page.ListingId, description, page.ApplyUrl);
            if (page.ApplyUrl is null) {
                ads.SetApplyStatus(page.ListingId, ApplyStatus.Skipped, runStarted);
            }
        }
    }
}
