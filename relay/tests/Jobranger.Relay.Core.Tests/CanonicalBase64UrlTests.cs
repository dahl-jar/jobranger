namespace Jobranger.Relay.Core.Tests;

public sealed class CanonicalBase64UrlTests
{
    private const int keyBytes = 32;

    [Fact]
    public void ShouldDecodeTextOfExpectedLength()
    {
        bool decoded = CanonicalBase64Url.TryDecode(ProofVector.PublicKey, keyBytes, out byte[] bytes);

        Assert.Equal((true, keyBytes), (decoded, bytes.Length));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHUR")]
    [InlineData("11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHURoA")]
    [InlineData("11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHURo=")]
    [InlineData("11qYAYKxCrfVS+7TyWQHOg7hcvPapiMlrwIaaPcHURo")]
    [InlineData("11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHURp")]
    public void ShouldRejectTextThatIsNotCanonicalForLength(string? text)
    {
        bool decoded = CanonicalBase64Url.TryDecode(text, keyBytes, out _);

        Assert.False(decoded);
    }
}
