using System.Text;
using Jobranger.Cli.Features.Sources;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Sources;

public sealed class FinnPagesTests {
    private const string brokenBlock = "<script type=\"application/ld+json\">{bad json</script>";
    private const string metadata = """{"result_size": {"match_count": 1}, "paging": {"last": 1}}""";
    private const byte invalidUtf8 = 0xFF;
    private const byte invalidUtf8Marker = (byte)'~';

    [Fact]
    public void ShouldReadSearchDocs_WhenPageIsFinnSearch() {
        FinnSearchPage page = FinnPages.ParseSearch(SourceTestData("finn_search.html"));

        Assert.Equal(
            (542, 50, FinnListing("400000001") with {
                Title = "Utvikler 1",
                Published = "2026-08-19T07:20:50+00:00",
                Deadline = "2026-09-13T21:59:59.999000+00:00",
            }),
            (page.MatchCount, page.Listings.Count, page.Listings[0]));
    }

    [Fact]
    public void ShouldThrowInvalidData_WhenSearchHasNoStateTag() {
        Assert.Throws<InvalidDataException>(() => FinnPages.ParseSearch("<html><body>Ingen treff</body></html>"));
    }

    [Fact]
    public void ShouldThrowInvalidData_WhenDocHasNoId() {
        string html = SearchHtml([new() { ["heading"] = "Utvikler" }], 1);

        Assert.Throws<InvalidDataException>(() => FinnPages.ParseSearch(html));
    }

    [Theory]
    [InlineData("lone surrogate id")]
    [InlineData("invalid utf-8 heading")]
    [InlineData("state not base64")]
    [InlineData("no query with docs")]
    [InlineData("docs not a list")]
    [InlineData("no metadata")]
    [InlineData("fractional match count")]
    public void ShouldThrowInvalidData_WhenSearchStateIsMalformed(string malformed) {
        string html = MalformedSearch(malformed);

        Assert.Throws<InvalidDataException>(() => FinnPages.ParseSearch(html));
    }

    [Theory]
    [InlineData(253402300799999, "9999-12-31T23:59:59.999000+00:00")]
    [InlineData(253402300800000, null)]
    public void ShouldKeepPublishedOnlyUpToYear9999_WhenEpochIsAtLimit(long epochMs, string? published) {
        string html = SearchHtml([SearchDoc(1, ("published", epochMs))], 1);

        Assert.Equal(published, Assert.Single(FinnPages.ParseSearch(html).Listings).Published);
    }

    [Fact]
    public void ShouldReadJobPostingText_WhenAdHasLdJson() {
        string text = FinnPages.ParseDescription(SourceTestData("finn_ad.html"));

        Assert.Equal((true, false), (text.Contains("Shopify", StringComparison.Ordinal), text.Contains("<p", StringComparison.Ordinal)));
    }

    [Fact]
    public void ShouldReadJobPostingText_WhenLdJsonRootIsList() {
        string html = """<script type="application/ld+json">[{"@type": "JobPosting", "description": "<p>Kode</p>"}]</script>""";

        Assert.Equal("Kode", FinnPages.ParseDescription(html));
    }

    [Fact]
    public void ShouldReadImportedText_WhenAdHasNoLdJson() {
        string text = FinnPages.ParseDescription(SourceTestData("finn_ad_imported.html"));

        Assert.Equal((true, false), (text.Contains("masteroppgave", StringComparison.Ordinal), text.Contains("<p", StringComparison.Ordinal)));
    }

    [Fact]
    public void ShouldReadNextBlock_WhenOneLdJsonBlockIsBroken() {
        string text = FinnPages.ParseDescription(brokenBlock + SourceTestData("finn_ad.html"));

        Assert.Contains("Shopify", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldThrowInvalidData_WhenAdHasNoText() {
        Assert.Throws<InvalidDataException>(() => FinnPages.ParseDescription(brokenBlock));
    }

    [Fact]
    public void ShouldReadApplyUrl_WhenFlightDataNamesOne() {
        string html = """self.__next_f.push([1,"\"applicationUrl\",\"https://jobs.example.test/apply/1\",\"company\""])""";

        Assert.Equal("https://jobs.example.test/apply/1", FinnPages.ParseApplyUrl(html));
    }

    [Fact]
    public void ShouldReturnNoApplyUrl_WhenFlightDataHasNone() {
        Assert.Null(FinnPages.ParseApplyUrl("<html>no apply link</html>"));
    }

    private static string MalformedSearch(string malformed) => malformed switch {
        "lone surrogate id" => StateHtml(State(Data("""[{"id": "\ud800"}]"""))),
        "state not base64" => "<html><script data-react-query-state=\"1\">not*base64</script></html>",
        "no query with docs" => StateHtml(State("""{"items": []}""")),
        "docs not a list" => StateHtml(State(Data("{}"))),
        "no metadata" => StateHtml(State("""{"docs": []}""")),
        "fractional match count" => StateHtml(State(
            """{"docs": [], "metadata": {"result_size": {"match_count": 1.5}, "paging": {"last": 1}}}""")),
        _ => StateHtml([.. State(Data("""[{"id": 1, "heading": "~"}]"""))
            .Select(part => part == invalidUtf8Marker ? invalidUtf8 : part)]),
    };

    private static string Data(string docs) => """{"docs": """ + docs + """, "metadata": """ + metadata + "}";

    private static byte[] State(string data) =>
        Encoding.UTF8.GetBytes("""{"queries": [{"state": {"data": """ + data + "}}]}");
}
