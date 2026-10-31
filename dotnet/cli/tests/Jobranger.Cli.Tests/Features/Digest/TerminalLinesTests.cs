using System.Collections;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class TerminalLinesTests {
    private const int longTitleLength = 30;
    private const int cutTitleLength = 21;

    private static readonly DigestCard matchCard = DigestFixtures.Card with {
        Title = "Data Engineer",
        DisplayTitle = null,
        Company = "Havbris Data AS",
        Location = "Trondheim",
        DeadlineDate = new DateOnly(2026, 10, 10),
        Fit = DigestFit.Strong,
    };

    [Fact]
    public void ShouldOrderHiddenPartsByCount_WhenCountsDiffer() {
        HiddenCount[] hidden = [new(HiddenGroup.Senior, 1), new(HiddenGroup.Masters, 3), new(HiddenGroup.Place, 2)];

        Assert.Equal(
            "6 hidden: 3 master's · 2 other places · 1 senior",
            DigestEntry.HiddenLine(hidden, null, DigestFixtures.English));
    }

    [Theory]
    [InlineData(2.5, "1 need 2+ yrs")]
    [InlineData(3.5, "1 need 4+ yrs")]
    public void ShouldRoundYearCapToEven_WhenCapIsHalf(double cap, string part) {
        string line = DigestEntry.HiddenLine([new(HiddenGroup.Years, 1)], cap, DigestFixtures.English);

        Assert.EndsWith(part, line, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldAskForMoreYears_WhenYearCapIsUnknown() {
        Assert.Equal(
            "2 hidden: 2 need more yrs",
            DigestEntry.HiddenLine([new(HiddenGroup.Years, 2)], null, DigestFixtures.English));
    }

    [Fact]
    public void ShouldReturnEmptyLine_WhenNothingIsHidden() {
        Assert.Equal("", DigestEntry.HiddenLine([new(HiddenGroup.Senior, 0)], null, DigestFixtures.English));
    }

    [Fact]
    public void ShouldUseSingularLabel_WhenOneAdIsHidden() {
        HiddenCount[] hidden = [new(HiddenGroup.Role, 1), new(HiddenGroup.Other, 2)];

        Assert.Equal(
            "3 hidden: 2 other reasons · 1 other role",
            DigestEntry.HiddenLine(hidden, null, DigestFixtures.English));
    }

    [Fact]
    public void ShouldUseNorwegianSingular_WhenOneAdIsHidden() {
        string line = DigestEntry.HiddenLine([new(HiddenGroup.Place, 1)], null, DigestFixtures.Norwegian);

        Assert.Equal("1 skjult: 1 annet sted", line);
    }

    [Theory]
    [InlineData("Strong", "  Strong fit    Data Engineer          Havbris Data AS   Trondheim   10 Oct")]
    [InlineData("Partial", "  Partial fit   Data Engineer          Havbris Data AS   Trondheim   10 Oct")]
    [InlineData("OnRequest", "  Shown on request Data Engineer          Havbris Data AS   Trondheim   10 Oct")]
    public void ShouldLineUpColumns_WhenMatchLineIsBuilt(string fit, string line) {
        DigestCard card = matchCard with { Fit = Enum.Parse<DigestFit>(fit) };

        Assert.Equal(line, DigestEntry.MatchLine(card, DigestFixtures.English));
    }

    [Fact]
    public void ShouldCutTitle_WhenTextPassesColumn() {
        DigestCard card = matchCard with { Title = new string('x', longTitleLength) };

        Assert.Equal(
            $"  Strong fit    {new string('x', cutTitleLength)}… Havbris Data AS   Trondheim   10 Oct",
            DigestEntry.MatchLine(card, DigestFixtures.English));
    }

    [Fact]
    public void ShouldCutCompany_WhenTextPassesColumn() {
        DigestCard card = matchCard with { Company = "Havbris Data og Energi AS" };

        Assert.Equal(
            "  Strong fit    Data Engineer          Havbris Data og … Trondheim   10 Oct",
            DigestEntry.MatchLine(card, DigestFixtures.English));
    }

    [Fact]
    public void ShouldCutLocation_WhenTextPassesColumn() {
        DigestCard card = matchCard with { Location = "Trondheim og Bergen" };

        Assert.Equal(
            "  Strong fit    Data Engineer          Havbris Data AS   Trondheim … 10 Oct",
            DigestEntry.MatchLine(card, DigestFixtures.English));
    }

    [Fact]
    public void ShouldPreferDisplayTitle_WhenCardHasOne() {
        DigestCard card = matchCard with { Title = "Utvikler", DisplayTitle = "Data Engineer" };

        Assert.Contains("Data Engineer", DigestEntry.MatchLine(card, DigestFixtures.English), StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldDropEscapeCharacters_WhenTitleHasThem() {
        DigestCard card = matchCard with { Title = "\u001b[2JData" };

        Assert.Equal(
            "  Strong fit    [2JData                Havbris Data AS   Trondheim   10 Oct",
            DigestEntry.MatchLine(card, DigestFixtures.English));
    }

    [Theory]
    [InlineData(Locales.English, "no date")]
    [InlineData(Locales.Norwegian, "ingen dato")]
    public void ShouldPrintNoDate_WhenCardHasNoDeadline(string lang, string date) {
        IMessages messages = Messages.For(null, new Hashtable { [Locales.LangVariable] = lang });
        DigestCard card = matchCard with { DeadlineDate = null };

        Assert.EndsWith(date, DigestEntry.MatchLine(card, messages), StringComparison.Ordinal);
    }
}
