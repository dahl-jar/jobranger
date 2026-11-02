using Jobranger.Cli.Config;

namespace Jobranger.Cli.Tests.Config;

public sealed class SmtpServerTests {
    [Theory]
    [InlineData("smtp://smtp.example.test", "smtp.example.test", 587, false)]
    [InlineData("smtps://smtp.example.test", "smtp.example.test", 465, true)]
    [InlineData("SMTP://smtp.example.test", "smtp.example.test", 587, false)]
    [InlineData("smtp://localhost:2525", "localhost", 2525, false)]
    [InlineData("smtp://smtp.example.test/", "smtp.example.test", 587, false)]
    [InlineData("smtp://smtp.example.test.:587", "smtp.example.test", 587, false)]
    [InlineData("smtp://SMTP.Example.TEST", "smtp.example.test", 587, false)]
    [InlineData("smtp://smtp.bø.example", "smtp.xn--b-5ga.example", 587, false)]
    [InlineData("smtp://smtp.example.test:65535", "smtp.example.test", 65535, false)]
    [InlineData("smtp://smtp.example.test:1", "smtp.example.test", 1, false)]
    [InlineData("smtp://[::1]:2525", "::1", 2525, false)]
    [InlineData("smtps://[::1]", "::1", 465, true)]
    public void ShouldReadServer_WhenUrlIsValid(string url, string host, int port, bool implicitTls) {
        SmtpServer? server = SmtpServer.Parse(url);

        Assert.Equal(new SmtpServer(host, port, implicitTls), server);
    }

    [Theory]
    [InlineData("http://smtp.example.test")]
    [InlineData("smtp.example.test")]
    [InlineData("smtp://[::1")]
    [InlineData("smtp://[::1]x25")]
    [InlineData("smtp://[127.0.0.1]")]
    [InlineData("smtp://[not-an-address]")]
    [InlineData("smtp://::1")]
    [InlineData("smtp://")]
    [InlineData("smtp://smtp.example.test/inbox")]
    [InlineData("smtp://user@smtp.example.test")]
    [InlineData("smtp://smtp.example.test:99999")]
    [InlineData("smtp://smtp.example.test:0")]
    [InlineData("smtp://smtp.example.test:25x")]
    [InlineData("smtp://smtp..example.test:587")]
    [InlineData("smtp://.:587")]
    [InlineData("smtp://exa mple.test:587")]
    [InlineData("smtp://a,b.example.test")]
    [InlineData("smtp://smtp.example.test..:587")]
    public void ShouldRejectSmtpUrl_WhenItIsNotBareServer(string url) {
        SmtpServer? server = SmtpServer.Parse(url);

        Assert.Null(server);
    }

    [Theory]
    [InlineData("smtp.gmail.com:465", "smtps://smtp.gmail.com:465")]
    [InlineData("mail.example.test:587", "smtp://mail.example.test:587")]
    [InlineData("mail.example.test", "smtp://mail.example.test")]
    public void ShouldPickScheme_WhenServerIsValid(string server, string url) {
        string? result = SmtpServer.UrlFor(server);

        Assert.Equal(url, result);
    }

    [Fact]
    public void ShouldReturnNull_WhenServerIsInvalid() {
        string? result = SmtpServer.UrlFor("mail example");

        Assert.Null(result);
    }
}
