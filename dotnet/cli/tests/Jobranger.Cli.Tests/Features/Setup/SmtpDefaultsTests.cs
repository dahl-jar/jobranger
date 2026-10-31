using Jobranger.Cli.Features.Setup;

namespace Jobranger.Cli.Tests.Features.Setup;

public sealed class SmtpDefaultsTests {
    [Theory]
    [InlineData("user@gmail.com", "smtp.gmail.com:465")]
    [InlineData("user@googlemail.com", "smtp.gmail.com:465")]
    [InlineData("user@example.test", "")]
    public void ShouldSuggestGmail_WhenAddressIsGmail(string address, string server) {
        string result = SmtpDefaults.Server(address);

        Assert.Equal(server, result);
    }
}
