using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Tests.Mocks;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class TerminalLinesTests {
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
}
