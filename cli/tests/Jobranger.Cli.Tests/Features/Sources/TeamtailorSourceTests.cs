using System.Text;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class TeamtailorSourceTests {
    private const string firstId = "teamtailor:example:100";
    private const string firstGuid = "<guid isPermaLink=\"false\">100</guid>";
    private const string firstTitle = "<title>Graduate Developer</title>";
    private const string firstLink = "<link>https://careers.example.test/jobs/100-graduate-developer</link>";
    private const string declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
    private const string roleElement = "<tt:role>Software Development</tt:role>";
    private const int hostileDepth = 100_000;

    private static readonly TeamtailorSource source = new(TeamtailorFeed);
    private static readonly string feedXml = SourceTestData("teamtailor_jobs.xml");

    [Fact]
    public async Task ShouldMapListingFields_WhenItemIsInNorway() {
        FakeSiteHandler handler = Feed(feedXml);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(new SourceListing {
            Id = firstId,
            Source = "teamtailor:example",
            SourceId = "100",
            Title = "Graduate Developer",
            Company = "Example AS",
            Location = "Oslo",
            Published = "2026-08-24T06:15:00+00:00",
            Url = "https://careers.example.test/jobs/100-graduate-developer",
            Description = "Build Java services.",
        }, Assert.Single(batch.Listings));
    }

    [Fact]
    public async Task ShouldSetListedIds_WhenFeedIsRead() {
        FakeSiteHandler handler = Feed(feedXml);

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(new HashSet<string> { firstId }, batch.ListedIds?.ToHashSet());
    }

    [Fact]
    public async Task ShouldReadTitle_WhenTeamtailorFeedIsLatin1() {
        string latin1 = feedXml
            .Replace("encoding=\"UTF-8\"", "encoding=\"ISO-8859-1\"", StringComparison.Ordinal)
            .Replace("Graduate Developer", "Utvikler på Ås", StringComparison.Ordinal);
        FakeSiteHandler handler = new(_ => Body(Encoding.Latin1.GetBytes(latin1)));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("Utvikler på Ås", Assert.Single(batch.Listings).Title);
    }

    [Fact]
    public async Task ShouldReadTitle_WhenLatin1FeedHasNoDeclaration() {
        string latin1 = feedXml
            .Replace(declaration, "", StringComparison.Ordinal)
            .Replace("Graduate Developer", "Utvikler på Ås", StringComparison.Ordinal);
        FakeSiteHandler handler = new(_ => Body(Encoding.Latin1.GetBytes(latin1)));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("Utvikler på Ås", Assert.Single(batch.Listings).Title);
    }

    [Fact]
    public async Task ShouldReadFeed_WhenDoctypeHasNoEntities() {
        string doctype = declaration + "<!DOCTYPE rss>";
        FakeSiteHandler handler = Feed(feedXml.Replace(declaration, doctype, StringComparison.Ordinal));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal(firstId, Ids(batch.Listings));
    }

    [Fact]
    public async Task ShouldFailWithoutPartial_WhenFeedIsNestedPastDepthLimit() {
        string nested = string.Concat(Enumerable.Repeat("<a>", hostileDepth))
            + string.Concat(Enumerable.Repeat("</a>", hostileDepth));
        FakeSiteHandler handler = Feed(feedXml.Replace(roleElement, nested, StringComparison.Ordinal));

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Null(error.Partial);
    }

    [Fact]
    public async Task ShouldUseLocationName_WhenCityIsMissing() {
        FakeSiteHandler handler = Feed(feedXml.Replace("<tt:city>Oslo</tt:city>", "", StringComparison.Ordinal));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Equal("Head office", Assert.Single(batch.Listings).Location);
    }

    [Theory]
    [InlineData(firstGuid, "")]
    [InlineData(firstTitle, "")]
    [InlineData(firstLink, "")]
    [InlineData(firstTitle, "<title>   </title>")]
    [InlineData(firstGuid, "<guid/>")]
    public async Task ShouldSkipItem_WhenRequiredFieldIsMissing(string field, string replacement) {
        FakeSiteHandler handler = Feed(feedXml.Replace(field, replacement, StringComparison.Ordinal));
        FakeLogger logger = new();

        SourceBatch batch = await source.FetchAsync(Context(handler, logger: logger), CancellationToken.None);

        Assert.Equal(("", "teamtailor.item.skipped"), (Ids(batch.Listings), Events(logger)));
    }

    [Fact]
    public async Task ShouldKeepItemWithoutDate_WhenPubDateIsInvalid() {
        FakeSiteHandler handler = Feed(feedXml.Replace("Mon, 24 Aug 2026 08:15:00 +0200", "snarest", StringComparison.Ordinal));

        SourceBatch batch = await source.FetchAsync(Context(handler), CancellationToken.None);

        Assert.Null(Assert.Single(batch.Listings).Published);
    }

    [Theory]
    [InlineData("<rss><broken>")]
    [InlineData("<feed><channel/></feed>")]
    [InlineData("<rss version=\"2.0\"/>")]
    [InlineData("<!DOCTYPE rss [<!ENTITY company \"Example\">]><rss><channel><title>&company;</title></channel></rss>")]
    [InlineData("<!DOCTYPE rss [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><rss><channel><title>&x;</title></channel></rss>")]
    [InlineData("<!DOCTYPE rss [<!ENTITY l0 \"ha\"><!ENTITY l1 \"&l0;&l0;&l0;&l0;&l0;&l0;&l0;&l0;\">"
        + "<!ENTITY l2 \"&l1;&l1;&l1;&l1;&l1;&l1;&l1;&l1;\">]><rss><channel><title>&l2;</title></channel></rss>")]
    public async Task ShouldFailWithoutPartial_WhenFeedIsNotRss(string body) {
        FakeSiteHandler handler = Feed(body);

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => source.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(("teamtailor:example failed", null), (error.Message, error.Partial));
    }

    [Fact]
    public async Task ShouldFail_WhenFeedUrlIsInvalid() {
        FakeSiteHandler handler = Feed(feedXml);
        TeamtailorSource broken = new(TeamtailorFeed with { Board = "not a url" });

        SourceException error = await Assert.ThrowsAsync<SourceException>(
            () => broken.FetchAsync(Context(handler), CancellationToken.None));

        Assert.Equal(("teamtailor:example failed", null, 0), (error.Message, error.Partial, handler.Urls.Count));
    }

    private static FakeSiteHandler Feed(string xml) => new(_ => Body(Encoding.UTF8.GetBytes(xml)));
}
