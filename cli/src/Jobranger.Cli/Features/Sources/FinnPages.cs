using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Sources;

internal static partial class FinnPages {
    public const string IdPrefix = "finn:";

    private const int matchTimeoutMs = 2000;
    private const string queriesKey = "queries";
    private const string stateKey = "state";
    private const string dataKey = "data";
    private const string docsKey = "docs";
    private const string metadataKey = "metadata";
    private const string resultSizeKey = "result_size";
    private const string matchCountKey = "match_count";
    private const string pagingKey = "paging";
    private const string lastKey = "last";
    private const string idKey = "id";
    private const string jobTitleKey = "job_title";
    private const string headingKey = "heading";
    private const string companyKey = "company_name";
    private const string locationKey = "location";
    private const string publishedKey = "published";
    private const string deadlineKey = "deadline";
    private const string canonicalUrlKey = "canonical_url";
    private const string ldJsonWrapperKey = "script:ld+json";
    private const string typeKey = "@type";
    private const string jobPostingType = "JobPosting";
    private const string descriptionKey = "description";

    private const long maxEpochMs = long.MaxValue / TimeSpan.MicrosecondsPerMillisecond;

    private static readonly long minMicros = (DateTimeOffset.MinValue - DateTimeOffset.UnixEpoch).Ticks
        / TimeSpan.TicksPerMicrosecond;
    private static readonly long maxMicros = (DateTimeOffset.MaxValue - DateTimeOffset.UnixEpoch).Ticks
        / TimeSpan.TicksPerMicrosecond;

    public static FinnSearchPage ParseSearch(string html) {
        Match tag = StateTag().Match(html);
        if (!tag.Success) {
            throw new InvalidDataException("data-react-query-state script tag not found");
        }
        string encoded = tag.Groups[1].Value.Trim();
        byte[] state = new byte[encoded.Length];
        if (!Convert.TryFromBase64String(encoded, state, out int written)) {
            throw new InvalidDataException("react-query state is not base64");
        }
        using JsonDocument document = JsonFields.Parse(state[..written]);
        foreach (JsonElement query in JsonFields.Objects(document.RootElement, queriesKey)) {
            if (JsonFields.Object(query, stateKey) is JsonElement queryState
                && JsonFields.Object(queryState, dataKey) is JsonElement data
                && data.TryGetProperty(docsKey, out _)) {
                return PageFrom(data);
            }
        }
        throw new InvalidDataException("no query with docs found in react-query state");
    }

    public static string ParseDescription(string html) {
        foreach (Match block in LdJsonBlock().Matches(html)) {
            if (PostingText(block.Groups[1].Value) is string posting) {
                return posting;
            }
        }
        Match imported = ImportDecoration().Match(html);
        if (imported.Success && HtmlText.Strip(imported.Groups[1].Value) is { Length: > 0 } text) {
            return text;
        }
        throw new InvalidDataException("no JobPosting ld+json or import-decoration text found");
    }

    public static string? ParseApplyUrl(string html) {
        Match match = ApplyUrl().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static FinnSearchPage PageFrom(JsonElement data) {
        JsonElement metadata = Required(data, metadataKey);
        int matchCount = Whole(Required(metadata, resultSizeKey), matchCountKey);
        int lastPage = Whole(Required(metadata, pagingKey), lastKey);
        if (data.GetProperty(docsKey).ValueKind != JsonValueKind.Array) {
            throw new InvalidDataException("docs is not a list");
        }
        List<SourceListing> listings = [];
        Dictionary<string, string> jobTitles = new(StringComparer.Ordinal);
        foreach (JsonElement doc in data.GetProperty(docsKey).EnumerateArray()) {
            SourceListing listing = Listing(doc);
            listings.Add(listing);
            jobTitles[listing.Id] = JsonFields.Text(doc, jobTitleKey) ?? string.Empty;
        }
        return new FinnSearchPage(listings, matchCount, jobTitles, lastPage);
    }

    private static SourceListing Listing(JsonElement doc) {
        string sourceId = SourceId(doc);
        return new SourceListing {
            Id = IdPrefix + sourceId,
            Source = FinnSource.SourceName,
            SourceId = sourceId,
            Title = JsonFields.Text(doc, headingKey) ?? string.Empty,
            Company = JsonFields.Text(doc, companyKey) ?? string.Empty,
            Location = JsonFields.Text(doc, locationKey) ?? string.Empty,
            Published = EpochText(doc, publishedKey),
            Deadline = EpochText(doc, deadlineKey),
            Url = JsonFields.Text(doc, canonicalUrlKey) ?? string.Empty,
        };
    }

    private static string SourceId(JsonElement doc) {
        if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty(idKey, out JsonElement id)
            && id.ValueKind == JsonValueKind.Number) {
            return id.GetRawText();
        }
        if (JsonFields.FilledText(doc, idKey) is string text) {
            return text;
        }
        throw new InvalidDataException("search doc has no id");
    }

    private static string? EpochText(JsonElement doc, string key) {
        if (!doc.TryGetProperty(key, out JsonElement value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out decimal epochMs) || Math.Abs(epochMs) > maxEpochMs) {
            return null;
        }
        long micros = (long)Math.Round(epochMs * TimeSpan.MicrosecondsPerMillisecond);
        if (micros < minMicros || micros > maxMicros) {
            return null;
        }
        return StoredTime.Format(DateTimeOffset.UnixEpoch.AddTicks(micros * TimeSpan.TicksPerMicrosecond));
    }

    private static string? PostingText(string block) {
        try {
            using JsonDocument document = JsonDocument.Parse(block);
            JsonElement outer = document.RootElement;
            JsonElement? inner = outer.ValueKind == JsonValueKind.Object
                ? outer.TryGetProperty(ldJsonWrapperKey, out JsonElement wrapped) ? wrapped : null
                : outer;
            IEnumerable<JsonElement> items = inner switch {
                { ValueKind: JsonValueKind.Array } list => list.EnumerateArray(),
                JsonElement item => [item],
                null => [],
            };
            foreach (JsonElement item in items) {
                if (JsonFields.Text(item, typeKey) == jobPostingType
                    && JsonFields.FilledText(item, descriptionKey) is string description
                    && HtmlText.Strip(description) is { Length: > 0 } text) {
                    return text;
                }
            }
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    private static JsonElement Required(JsonElement parent, string key) =>
        JsonFields.Object(parent, key) ?? throw new InvalidDataException($"search state has no {key}");

    private static int Whole(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int number)
            ? number
            : throw new InvalidDataException($"search state {key} is not a whole number");

    [GeneratedRegex("<script[^>]*data-react-query-state[^>]*>([^<]+)</script>", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex StateTag();

    [GeneratedRegex("""<script type="application/ld\+json">(.*?)</script>""", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex LdJsonBlock();

    [GeneratedRegex("""<div[^>]*class="[^"]*import-decoration[^"]*"[^>]*>(.*?)</section>""", RegexOptions.Singleline,
        matchTimeoutMs)]
    private static partial Regex ImportDecoration();

    [GeneratedRegex("""\\"applicationUrl\\",\\"(https?://[^\\"]+)""", RegexOptions.None, matchTimeoutMs)]
    private static partial Regex ApplyUrl();
}
