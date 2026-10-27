using System.Text;
using System.Text.Unicode;
using System.Xml;
using System.Xml.Linq;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Sources;

internal sealed class TeamtailorSource(EmployerFeed feed) : ISource {
    private const string rssName = "rss";
    private const string channelName = "channel";
    private const string itemName = "item";
    private const string guidName = "guid";
    private const string titleName = "title";
    private const string linkName = "link";
    private const string descriptionName = "description";
    private const string pubDateName = "pubDate";
    private const string locationName = "location";
    private const string countryName = "country";
    private const string cityName = "city";
    private const string placeName = "name";
    private const int maxDepth = 32;
    private const int nodesPerCancelCheck = 1024;

    private static readonly SourceRequestOptions options = new();
    private static readonly XmlReaderSettings readerSettings = new() {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        CloseInput = true,
    };

    public string Name => feed.Source;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(feed.Board, UriKind.Absolute, out Uri? url)) {
            throw new SourceException($"{feed.Source} failed", null);
        }
        try {
            byte[] body = await SourceHttp.GetAsync(context.Http, url, options, cancellationToken).ConfigureAwait(false);
            return ParseFeed(Load(body, cancellationToken), context.Logger);
        } catch (Exception error) when (SourceHttp.IsFailure(error, cancellationToken)) {
            throw new SourceException($"{feed.Source} failed", null, error);
        }
    }

    private static XDocument Load(byte[] body, CancellationToken cancellationToken) {
        try {
            return Decode(body, cancellationToken);
        } catch (XmlException error) {
            throw new InvalidDataException("teamtailor returned malformed XML", error);
        }
    }

    private static XDocument Decode(byte[] body, CancellationToken cancellationToken) {
        try {
            return Read(() => XmlReader.Create(new MemoryStream(body, false), readerSettings), cancellationToken);
        } catch (XmlException) when (!Utf8.IsValid(body)) {
            string latin1 = Encoding.Latin1.GetString(body);
            return Read(() => XmlReader.Create(new StringReader(latin1), readerSettings), cancellationToken);
        }
    }

    private static XDocument Read(Func<XmlReader> open, CancellationToken cancellationToken) {
        using (XmlReader scan = open()) {
            CheckDepth(scan, cancellationToken);
        }
        using XmlReader reader = open();
        return XDocument.Load(reader);
    }

    private static void CheckDepth(XmlReader reader, CancellationToken cancellationToken) {
        for (long nodes = 0; reader.Read(); nodes++) {
            if (nodes % nodesPerCancelCheck == 0) {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (reader.Depth > maxDepth) {
                throw new InvalidDataException($"teamtailor feed nests deeper than {maxDepth} levels");
            }
        }
    }

    private SourceBatch ParseFeed(XDocument document, ILogger logger) {
        XElement? root = document.Root;
        XElement? channel = root?.Element(channelName);
        if (root?.Name.LocalName != rssName || channel is null) {
            throw new InvalidDataException("teamtailor feed has no RSS channel");
        }
        List<SourceListing> listings = [];
        foreach (XElement item in channel.Elements(itemName)) {
            string? sourceId = ChildText(item, guidName);
            string? title = ChildText(item, titleName);
            string? link = ChildText(item, linkName);
            if (sourceId is null || title is null || link is null) {
                LogEvents.TeamtailorItemSkipped(logger, feed.Source);
                continue;
            }
            List<string> locations = NorwegianLocations(item);
            if (locations.Count > 0) {
                listings.Add(ToListing(item, sourceId, title, link, locations));
            }
        }
        return FeedBatch.Of(feed, listings);
    }

    private SourceListing ToListing(
        XElement item, string sourceId, string title, string link, IReadOnlyList<string> locations) => new() {
            Id = FeedBatch.Id(feed, sourceId),
            Source = feed.Source,
            SourceId = sourceId,
            Title = title,
            Company = feed.Company,
            Location = FeedBatch.Location(locations),
            Published = ChildText(item, pubDateName) is string date && MailDates.Parse(date) is DateTimeOffset published
                ? StoredTime.Format(published)
                : null,
            Url = link,
            Description = HtmlText.Strip(ChildText(item, descriptionName) ?? string.Empty),
        };

    private static List<string> NorwegianLocations(XElement item) {
        List<string> locations = [];
        foreach (XElement location in item.Descendants().Where(element => element.Name.LocalName == locationName)) {
            if (FeedCountries.IsNorway(ChildText(location, countryName))
                && (ChildText(location, cityName) ?? ChildText(location, placeName)) is string city) {
                locations.Add(city);
            }
        }
        return locations;
    }

    private static string? ChildText(XElement element, string name) {
        foreach (XElement child in element.Elements().Where(child => child.Name.LocalName == name)) {
            List<XText> leading = [.. child.Nodes().TakeWhile(node => node is not XElement).OfType<XText>()];
            if (leading.Count == 0) {
                continue;
            }
            string value = string.Concat(leading.Select(text => text.Value)).Trim();
            return value.Length > 0 ? value : null;
        }
        return null;
    }
}
