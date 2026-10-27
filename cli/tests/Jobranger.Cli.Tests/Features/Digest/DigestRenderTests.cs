using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class DigestRenderTests {
    private const string goldenFolder = "Digest";
    private const string site = "https://site.example.test";
    private const int maxBytes = 90_000;
    private const int sizeCards = 60;
    private const int summaryCopies = 70;
    private const string cardMarker = "table-layout:fixed";

    private static readonly DigestData data = DigestFixtures.Data;

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

    [Fact]
    public void ShouldDropWholeCards_WhenDigestPassesSizeLimit() {
        string summary = string.Concat(Enumerable.Repeat("Bygg tjenester. ", summaryCopies));
        DigestCard[] cards = [
            .. Enumerable.Range(1, sizeCards).Select(index => DigestFixtures.Card with { Id = $"finn:{index}", Summary = summary }),
        ];

        RenderedDigest rendered = DigestEntry.Render(data with { NewToday = cards }, site, DigestFixtures.Norwegian);

        int kept = rendered.AdIds.Count;
        Assert.InRange(Encoding.UTF8.GetByteCount(rendered.Html), 0, maxBytes);
        Assert.InRange(kept, 1, sizeCards - 1);
        Assert.Equal(kept, rendered.Html.Split(cardMarker).Length - 1);
        Assert.Contains($"{sizeCards - kept} til på nettsiden", rendered.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(11, "this morning")]
    [InlineData(12, "this afternoon")]
    [InlineData(17, "this afternoon")]
    [InlineData(18, "this evening")]
    public void ShouldNamePartOfDay_WhenRunEndsAtHour(int hour, string when) {
        DigestData atHour = data with { RanAt = DigestFixtures.RanAt.AddHours(hour - DigestFixtures.RanAt.Hour) };

        Assert.Contains(when, DigestEntry.Render(atHour, null, DigestFixtures.English).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5, "overnight")]
    [InlineData(6, "today")]
    public void ShouldSayReadOvernight_WhenRunEndsBeforeSix(int hour, string when) {
        DigestData atHour = data with { RanAt = DigestFixtures.RanAt.AddHours(hour - DigestFixtures.RanAt.Hour) };

        string text = DigestEntry.Render(atHour, null, DigestFixtures.English).Text;

        Assert.Contains($"Read FINN and NAV {when}.", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2012, true)]
    [InlineData(2013, false)]
    public void ShouldKeepActionLinks_WhenAdLinkIsAtLimit(int idLength, bool kept) {
        DigestData longId = data with { NewToday = [DigestFixtures.Card with { Id = new string('x', idLength) }] };

        RenderedDigest rendered = DigestEntry.Render(longId, site, DigestFixtures.English);

        Assert.Equal((kept, 1), (rendered.Text.Contains("Mark applied:", StringComparison.Ordinal), rendered.AdIds.Count));
    }

    [Fact]
    public void ShouldWriteNorwegianText_WhenMessagesAreNorwegian() {
        string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Digest", "norwegian.txt"));

        Assert.Equal(expected, DigestEntry.Render(data, null, DigestFixtures.Norwegian).Text);
    }
}
