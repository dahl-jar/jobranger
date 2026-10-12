namespace Jobranger.Relay.Core.Tests;

public sealed class PairingCodeTests
{
    [Theory]
    [InlineData("kqfb-mxtw")]
    [InlineData("kqfb mxtw")]
    public void ShouldParseCodeTypedWithSeparatorInLowercase(string input)
    {
        bool parsed = PairingCode.TryParse(input, out PairingCode code);

        Assert.True(parsed);
        Assert.Equal(new PairingCode("KQFB", "MXTW"), code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("K7QF-2M9X")]
    [InlineData("KQFB-MXT")]
    [InlineData("KQFB-MXTWB")]
    public void ShouldRejectMalformedCode(string? input)
    {
        bool parsed = PairingCode.TryParse(input, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void ShouldGenerateEightCharactersFromAlphabet()
    {
        PairingCode code = PairingCode.Generate();

        Assert.Matches("^[BCDFGHJKLMNPQRSTVWXZ]{4}-[BCDFGHJKLMNPQRSTVWXZ]{4}$", code.ToString());
    }
}
