using Jobranger.Cli.Features.Setup;

namespace Jobranger.Cli.Tests.Features.Setup;

public sealed class SmtpDefaultsTests {
    [Theory]
    [InlineData("smtp.gmail.com:465", "smtps://smtp.gmail.com:465")]
    [InlineData("mail.example.test:587", "smtp://mail.example.test:587")]
    [InlineData("mail.example.test", "smtp://mail.example.test")]
    [InlineData("[::1]:465", "smtps://[::1]:465")]
    public void ShouldPickScheme_WhenPortIsGiven(string server, string url) {
        string? result = SmtpDefaults.Url(server);

        Assert.Equal(url, result);
    }

    [Theory]
    [InlineData("mail example")]
    [InlineData("host:x")]
    public void ShouldReturnNull_WhenServerIsInvalid(string server) {
        string? result = SmtpDefaults.Url(server);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("user@gmail.com", "smtp.gmail.com:465")]
    [InlineData("user@googlemail.com", "smtp.gmail.com:465")]
    [InlineData("user@example.test", "")]
    public void ShouldSuggestGmail_WhenAddressIsGmail(string address, string server) {
        string result = SmtpDefaults.Server(address);

        Assert.Equal(server, result);
    }
}
