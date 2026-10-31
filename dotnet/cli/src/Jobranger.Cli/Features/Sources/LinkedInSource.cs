using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Text;
using Jobranger.Cli.Http;
using Jobranger.Cli.Text;
using static Jobranger.Cli.Features.Sources.LinkedInPages;

namespace Jobranger.Cli.Features.Sources;

internal sealed class LinkedInSource : ISource {
    public const string SourceName = "linkedin";
    public const string SinceKey = "linkedin_since";

    private const string searchUrl = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search";
    private const string postingUrl = "https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/";
    private const string viewUrl = "https://www.linkedin.com/jobs/view/";
    private const string norwayGeoId = "103819153";
    private const string idPrefix = "linkedin:";
    private const int pageSize = 10;
    private const int maxPages = 10;

    private static readonly FrozenSet<string> keptEmploymentTypes =
        FrozenSet.Create(StringComparer.Ordinal, "Full-time", "Internship", "Temporary");
    private static readonly SourceRequestOptions options = new();

    public string Name => SourceName;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        long windowSeconds = (long)(context.RunStarted - SourceCursors.Start(context, SinceKey)).TotalSeconds;
        List<SourceListing> listings = [];
        try {
            IReadOnlyList<Card> cards = await SearchAllAsync(context, windowSeconds, cancellationToken)
                .ConfigureAwait(false);
            foreach (Card card in cards.Where(card => IsWanted(context, card))) {
                Posting? posting = await PostingAsync(context.Http, card.JobId, cancellationToken).ConfigureAwait(false);
                if (posting is not null && IsKept(posting)) {
                    listings.Add(ToListing(card, posting));
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Batch(listings, null) with { Partial = true };
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            throw new SourceException("linkedin search failed", Batch(listings, null), error);
        }
        return Batch(listings, SourceCursors.Advanced(context.RunStarted));
    }

    private static async Task<IReadOnlyList<Card>> SearchAllAsync(
        FetchContext context, long windowSeconds, CancellationToken cancellationToken) {
        List<Card> cards = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string keyword in context.Plan.LinkedInKeywords) {
            for (int page = 0; page < maxPages; page++) {
                IReadOnlyList<Card> found = await SearchPageAsync(
                    context.Http, keyword, page * pageSize, windowSeconds, cancellationToken).ConfigureAwait(false);
                if (found.Count == 0) {
                    break;
                }
                cards.AddRange(found.Where(card => seen.Add(card.JobId)));
            }
        }
        return cards;
    }

    private static async Task<IReadOnlyList<Card>> SearchPageAsync(
        HttpClient client, string keyword, int start, long windowSeconds, CancellationToken cancellationToken) {
        string query = string.Create(CultureInfo.InvariantCulture,
            $"keywords={Uri.EscapeDataString(keyword)}&geoId={norwayGeoId}&f_TPR=r{windowSeconds}&start={start}");
        byte[] body = await SourceHttp.GetAsync(client, new Uri($"{searchUrl}?{query}"), options, cancellationToken)
            .ConfigureAwait(false);
        return LinkedInPages.ParseSearch(Encoding.UTF8.GetString(body));
    }

    private static async Task<Posting?> PostingAsync(HttpClient client, string jobId, CancellationToken cancellationToken) {
        try {
            byte[] body = await SourceHttp.GetAsync(client, new Uri(postingUrl + jobId), options, cancellationToken)
                .ConfigureAwait(false);
            return LinkedInPages.ParsePosting(Encoding.UTF8.GetString(body));
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken) && !IsRateLimited(error)) {
            return null;
        }
    }

    private static bool IsRateLimited(Exception error) =>
        error is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };

    private static bool IsWanted(FetchContext context, Card card) =>
        !context.KnownIds.Contains(idPrefix + card.JobId)
        && context.Plan.TitleMatches(card.Title)
        && !(TitleLevels.Of(card.Title) is string level && context.Plan.SkipTitleLevels.Contains(level));

    private static bool IsKept(Posting posting) =>
        string.IsNullOrEmpty(posting.EmploymentType) || keptEmploymentTypes.Contains(posting.EmploymentType);

    private static SourceListing ToListing(Card card, Posting posting) => new() {
        Id = idPrefix + card.JobId,
        Source = SourceName,
        SourceId = card.JobId,
        Title = card.Title,
        Company = card.Company,
        Location = card.Location,
        Published = card.Published,
        Url = viewUrl + card.JobId,
        Description = posting.Description,
        ApplyUrl = viewUrl + card.JobId,
    };

    private static SourceBatch Batch(IReadOnlyList<SourceListing> listings, string? cursor) => new() {
        Source = SourceName,
        Listings = [.. listings],
        SeenIds = new HashSet<string>(listings.Select(listing => listing.Id), StringComparer.Ordinal),
        Cursors = cursor is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [SinceKey] = cursor },
    };
}
