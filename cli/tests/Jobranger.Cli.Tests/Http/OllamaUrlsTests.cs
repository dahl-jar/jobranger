using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Http;

public sealed class OllamaUrlsTests {
    private const string invalidUrl = "http://[::1";
    private const string ollamaUrl = "http://box:11434";

    [Theory]
    [InlineData("http://v1", "http://v1")]
    [InlineData("http://v1:11434/v1", "http://v1:11434")]
    [InlineData("http://box:11434/full/v1/v1", "http://box:11434/full")]
    [InlineData("http://box:11434/full//v1", "http://box:11434/full")]
    [InlineData("http://box:11434/proxyX", "http://box:11434/proxyX")]
    [InlineData("http://box:11434/proxyX/v1/", "http://box:11434/proxyX")]
    [InlineData(invalidUrl, invalidUrl)]
    public void ShouldStripApiPath_WhenUrlPathEndsWithIt(string url, string root) {
        Assert.Equal(root, OllamaUrls.Root(url));
    }

    [Theory]
    [InlineData("http://user:Secret42@box:11434/v1", "http://box:11434/v1")]
    [InlineData("http://user@box:11434", ollamaUrl)]
    [InlineData("http://user:p@ss@box:11434", ollamaUrl)]
    [InlineData("http://user:Qa/Secret#Pw42@box:11434", ollamaUrl)]
    [InlineData("http://user:Qa?Secret@box:11434", ollamaUrl)]
    [InlineData("http:/user:Secret@box:11434", "http:/box:11434")]
    [InlineData("HTTP://user:Qa/Secret@box:11434", "HTTP://box:11434")]
    [InlineData("http://user:pass@[::1", invalidUrl)]
    [InlineData(ollamaUrl, ollamaUrl)]
    [InlineData("box:11434", "box:11434")]
    public void ShouldHideUserinfo_WhenShowingUrl(string url, string shown) {
        Assert.Equal(shown, OllamaUrls.WithoutUserinfo(url));
    }

    [Fact]
    public void ShouldSendEmptyPassword_WhenUserinfoHasNoPassword() {
        Assert.Equal("Basic dXNlcjo=", OllamaUrls.BasicAuth("http://user@box:11434")?.ToString());
    }

    [Fact]
    public void ShouldReturnRoot_WhenUrlIsValid() {
        Assert.Equal(ollamaUrl, OllamaUrls.CheckedRoot(ollamaUrl + "/v1/"));
    }

    [Theory]
    [InlineData("http://user:Secret42@[::1", "ollama_url 'http://[::1' is not an http or https URL with a host")]
    [InlineData("ftp://box:11434", "ollama_url 'ftp://box:11434' is not an http or https URL with a host")]
    public void ShouldThrowConnectionError_WhenUrlIsNotHttp(string url, string message) {
        ApiException error = Assert.Throws<ApiException>(() => OllamaUrls.CheckedRoot(url));

        Assert.Equal((ApiErrorKind.Connection, message), (error.Kind, error.Message));
    }
}
