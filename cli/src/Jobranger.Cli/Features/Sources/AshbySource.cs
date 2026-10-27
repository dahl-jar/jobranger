using System.Text.Json;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Sources;

internal sealed class AshbySource(EmployerFeed feed) : ISource {
    public const string Kind = "ashby";

    private const string boardUrl = "https://api.ashbyhq.com/posting-api/job-board/";
    private const string jobsKey = "jobs";
    private const string idKey = "id";
    private const string titleKey = "title";
    private const string jobUrlKey = "jobUrl";
    private const string isListedKey = "isListed";
    private const string publishedKey = "publishedAt";
    private const string descriptionKey = "descriptionHtml";
    private const string addressKey = "address";
    private const string secondaryKey = "secondaryLocations";
    private const string locationKey = "location";
    private const string postalKey = "postalAddress";
    private const string countryKey = "addressCountry";
    private const string localityKey = "addressLocality";

    private static readonly SourceRequestOptions options = new();

    public string Name => feed.Source;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        try {
            Uri url = new(boardUrl + Uri.EscapeDataString(feed.Board));
            byte[] body = await SourceHttp.GetAsync(context.Http, url, options, cancellationToken).ConfigureAwait(false);
            return ParseBoard(body, context.Logger);
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            throw new SourceException($"{feed.Source} failed", null, error);
        }
    }

    private SourceBatch ParseBoard(byte[] body, ILogger logger) {
        using JsonDocument document = JsonFields.Parse(body);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(jobsKey, out JsonElement jobs)
            || jobs.ValueKind != JsonValueKind.Array) {
            throw new InvalidDataException("ashby payload has no jobs list");
        }
        List<SourceListing> listings = [];
        foreach (JsonElement job in jobs.EnumerateArray()) {
            Parsed parsed = ParseJob(job);
            if (parsed.Listing is not null) {
                listings.Add(parsed.Listing);
            }
            if (parsed.Bad) {
                LogEvents.AshbyJobSkipped(logger, feed.Source);
            }
        }
        return FeedBatch.Of(feed, listings);
    }

    private Parsed ParseJob(JsonElement job) {
        if (Required(job, idKey) is not string sourceId || Required(job, titleKey) is not string title
            || Required(job, jobUrlKey) is not string url
            || !job.TryGetProperty(isListedKey, out JsonElement listed)
            || listed.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) {
            return new Parsed(null, true);
        }
        List<string> locations = NorwegianLocations(job);
        if (!listed.GetBoolean() || locations.Count == 0) {
            return new Parsed(null, false);
        }
        return new Parsed(new SourceListing {
            Id = FeedBatch.Id(feed, sourceId),
            Source = feed.Source,
            SourceId = sourceId,
            Title = title,
            Company = feed.Company,
            Location = FeedBatch.Location(locations),
            Published = JsonFields.Text(job, publishedKey),
            Url = url,
            Description = JsonFields.Text(job, descriptionKey) is string html ? HtmlText.Strip(html) : string.Empty,
        }, false);
    }

    private static string? Required(JsonElement job, string key) =>
        JsonFields.Text(job, key)?.Trim() is { Length: > 0 } text ? text : null;

    private static List<string> NorwegianLocations(JsonElement job) {
        List<string> locations = [];
        JsonElement? address = JsonFields.Object(job, addressKey);
        AddNorwegian(locations, address);
        foreach (JsonElement secondary in JsonFields.Objects(job, secondaryKey)) {
            AddNorwegian(locations, JsonFields.Object(secondary, addressKey));
        }
        if (locations.Count == 0 && FeedCountries.IsNorway(Postal(address, countryKey))
            && Required(job, locationKey) is string fallback) {
            locations.Add(fallback);
        }
        return locations;
    }

    private static void AddNorwegian(List<string> locations, JsonElement? address) {
        if (FeedCountries.IsNorway(Postal(address, countryKey))
            && Postal(address, localityKey)?.Trim() is { Length: > 0 } locality) {
            locations.Add(locality);
        }
    }

    private static string? Postal(JsonElement? address, string key) =>
        address is JsonElement value && JsonFields.Object(value, postalKey) is JsonElement postal
            ? JsonFields.Text(postal, key)
            : null;

    private sealed record Parsed(SourceListing? Listing, bool Bad);
}
