using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class DigestRenderTests {
    private const string goldenFolder = "Digest";
    private const string site = "https://site.example.test";
    private const string norwegianFolder = "TestData/Digest/Norwegian";
    private const int cardsAtLimit = 36;

    private static readonly DigestData data = DigestFixtures.Data;
    private static readonly string[] sourceNames = ["a", "b", "c", "d", "e", "f"];

    public static TheoryData<string> Cases() => [.. DigestCases.Names()];

    [Theory]
    [MemberData(nameof(Cases))]
    public void ShouldEqualGolden_WhenRenderingDigestCase(string name) {
        (DigestData caseData, string? siteUrl) = DigestCases.Load(name);
        JsonObject expected = JsonNode.Parse(MatchingFixtures.GoldenText(goldenFolder, "subjects.json"))![name]!.AsObject();

        RenderedDigest rendered = DigestEntry.Render(caseData, siteUrl, DigestFixtures.English);

        Assert.Equal(MatchingFixtures.GoldenText(goldenFolder, $"{name}.html"), rendered.Html);
        Assert.Equal(MatchingFixtures.GoldenText(goldenFolder, $"{name}.txt"), rendered.Text);
        Assert.Equal(expected["subject"]!.GetValue<string>(), rendered.Subject);
        Assert.Equal(expected["ad_ids"]!.AsArray().Select(id => id!.GetValue<string>()), rendered.AdIds);
    }

    [Theory]
    [InlineData(11, "this morning")]
    [InlineData(12, "this afternoon")]
    [InlineData(17, "this afternoon")]
    [InlineData(18, "this evening")]
    public void ShouldNamePartOfDay_WhenRunEndsAtHour(int hour, string when) {
        DigestData atHour = data with { RanAt = DigestFixtures.RanAt.AddHours(hour - DigestFixtures.RanAt.Hour) };

        RenderedDigest rendered = DigestEntry.Render(atHour, null, DigestFixtures.English);

        Assert.Contains(when, rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5, "overnight")]
    [InlineData(6, "today")]
    public void ShouldSayReadOvernight_WhenRunEndsBeforeSix(int hour, string when) {
        DigestData atHour = data with { RanAt = DigestFixtures.RanAt.AddHours(hour - DigestFixtures.RanAt.Hour) };

        RenderedDigest rendered = DigestEntry.Render(atHour, null, DigestFixtures.English);

        Assert.Contains($"Read FINN and NAV {when}.", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2012, true)]
    [InlineData(2013, false)]
    public void ShouldKeepActionLinks_WhenAdLinkIsAtLimit(int idLength, bool kept) {
        DigestData longId = data with { NewToday = [DigestFixtures.Card with { Id = new string('x', idLength) }] };

        RenderedDigest rendered = DigestEntry.Render(longId, site, DigestFixtures.English);

        Assert.Equal((kept, 1), (rendered.Text.Contains("Mark applied:", StringComparison.Ordinal), rendered.AdIds.Count));
    }

    [Theory]
    [InlineData(5, "Could not read a, b, c, d and e.")]
    [InlineData(6, "Could not read a, b, c, d, e and 1 more.")]
    public void ShouldListAtMostFiveFailedSources_WhenSourcesFail(int failed, string note) {
        DigestData run = data with { FailedSources = [.. sourceNames.Take(failed)] };

        RenderedDigest rendered = DigestEntry.Render(run, null, DigestFixtures.English);

        Assert.Contains(note, rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldShowDisplayTitle_WhenAdHasOne() {
        DigestData titled = data with { NewToday = [DigestFixtures.Card with { DisplayTitle = "Shown Title" }] };

        RenderedDigest rendered = DigestEntry.Render(titled, null, DigestFixtures.English);

        Assert.Contains("Company One\nShown Title\n", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldLeaveOutSummary_WhenAdHasNone() {
        DigestData bare = data with { NewToday = [DigestFixtures.Card with { Summary = null }] };

        RenderedDigest rendered = DigestEntry.Render(bare, null, DigestFixtures.English);

        Assert.Contains("FINN\nOpen the ad", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("FINN</div><div style=\"margin-top:16px;", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldWriteOnlyStopLine_WhenThereAreNoNotes() {
        DigestData quiet = data with { SourcesRead = [] };

        RenderedDigest rendered = DigestEntry.Render(quiet, null, DigestFixtures.English);

        Assert.Contains("\">Stop these emails: jobranger config edit</p>", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldPointDroppedCardsToReport_WhenSiteIsOff() {
        (DigestData caseData, string? _) = DigestCases.Load("cut-for-size");

        RenderedDigest rendered = DigestEntry.Render(caseData, null, DigestFixtures.English);

        Assert.Contains("more in jobranger report", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(352, cardsAtLimit)]
    [InlineData(353, cardsAtLimit - 1)]
    public void ShouldKeepLastCard_WhenDigestIsAtSizeLimit(int twoByteLetters, int kept) {
        string fullSummary = new('a', CardText.SummaryLimit);
        string paddedSummary = new string('\u00E9', twoByteLetters) + fullSummary[twoByteLetters..];
        DigestCard[] cards = [
            .. Enumerable.Range(1, cardsAtLimit).Select(index => DigestFixtures.Card with {
                Id = $"finn:{index}",
                Summary = index == cardsAtLimit ? paddedSummary : fullSummary,
            }),
        ];

        RenderedDigest rendered = DigestEntry.Render(data with { NewToday = cards }, null, DigestFixtures.English);

        Assert.Equal(kept, rendered.AdIds.Count);
    }

    [Fact]
    public void ShouldLeaveOutSiteLinks_WhenSiteUrlIsRejected() {
        RenderedDigest rendered = DigestEntry.Render(data, "javascript:alert(1)", DigestFixtures.English);

        Assert.DoesNotContain("Mark applied:", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("/app", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ShouldWriteNorwegianText_WhenRenderingDigestCase(string name) {
        (DigestData caseData, string? siteUrl) = DigestCases.Load(name);
        string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, norwegianFolder, $"{name}.txt"));

        RenderedDigest rendered = DigestEntry.Render(caseData, siteUrl, DigestFixtures.Norwegian);

        Assert.Equal(expected, rendered.Text);
    }
}
