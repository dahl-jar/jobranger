using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class MatchRulesTests {
    [Theory]
    [InlineData(1, "0-1")]
    [InlineData(1.5, "1-3")]
    [InlineData(6, "3-6")]
    [InlineData(7, "6+")]
    public void ShouldPlaceYearsInBand_WhenBandCapAllowsThem(double years, string band) {
        Assert.Equal(band, MatchRules.BandOf(years));
    }

    [Theory]
    [InlineData(new[] { "0-1", "3-6" }, 6.0)]
    [InlineData(new[] { "0-1", "6+" }, null)]
    [InlineData(new string[0], 0.0)]
    public void ShouldTakeHighestBandCap_WhenReadingMaxYears(string[] years, double? expected) {
        Assert.Equal(expected, MatchingEntry.MaxYears(new Limits { Years = years }));
    }
}
