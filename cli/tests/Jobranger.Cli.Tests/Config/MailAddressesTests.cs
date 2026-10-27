using Jobranger.Cli.Config;

namespace Jobranger.Cli.Tests.Config;

public sealed class MailAddressesTests {
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last+tag@example.com")]
    [InlineData("user@bø.example")]
    public void ShouldAcceptAddress_WhenItIsOneAddress(string address) {
        bool valid = MailAddresses.IsAddress(address);

        Assert.True(valid);
    }

    [Theory]
    [InlineData("user@example.com\r\nX-Extra: 1")]
    [InlineData("user.example.com")]
    [InlineData("usér@example.com")]
    [InlineData("user@other@example.com")]
    [InlineData("user..name@example.com")]
    [InlineData("user@example..com")]
    [InlineData("user@a,b.example")]
    public void ShouldRejectAddress_WhenItIsNotOneAddress(string address) {
        bool valid = MailAddresses.IsAddress(address);

        Assert.False(valid);
    }

    [Theory]
    [InlineData("jobranger <user@example.test>")]
    [InlineData("Søker <user@example.com>")]
    [InlineData("user@example.com")]
    [InlineData("\"Job, Ranger\" <user@example.com>")]
    [InlineData("<user@example.com>")]
    [InlineData("\"\" <user@example.com>")]
    public void ShouldAcceptSender_WhenItIsOneMailbox(string sender) {
        bool valid = MailAddresses.IsMailbox(sender);

        Assert.True(valid);
    }

    [Theory]
    [InlineData(" jobranger <user@example.com>")]
    [InlineData("job\r\nranger <user@example.com>")]
    [InlineData("a@b.test,c@d.test")]
    [InlineData("jobranger <usér@example.com>")]
    [InlineData("jobranger <user@example.com")]
    [InlineData("\"Job\"Ranger\" <user@example.com>")]
    [InlineData("\"Job\\Ranger\" <user@example.com>")]
    [InlineData("\"Job\u0007\" <user@example.com>")]
    [InlineData("\"Job <user@example.com>")]
    public void ShouldRejectSender_WhenItIsNotOneMailbox(string sender) {
        bool valid = MailAddresses.IsMailbox(sender);

        Assert.False(valid);
    }
}
