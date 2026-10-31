using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Tests.Fields;

public sealed class Base64FieldsTests {
    private const int maxBytes = 3;

    [Fact]
    public void ShouldDecodeBase64_WhenEncodingIsCanonical() {
        byte[] decoded = Base64Fields.Decode("AAEC", maxBytes);

        Assert.Equal([0, 1, 2], decoded);
    }

    [Theory]
    [InlineData("AAB=")]
    [InlineData("AA AA")]
    [InlineData("AA")]
    [InlineData("AA-_")]
    public void ShouldRejectBase64_WhenEncodingIsNotCanonical(string text) {
        ContractException error = Assert.Throws<ContractException>(() => Base64Fields.Decode(text, maxBytes));

        Assert.Equal("base64", error.Keyword);
    }

    [Fact]
    public void ShouldRejectBase64_WhenDecodedBytesExceedMax() {
        ContractException error = Assert.Throws<ContractException>(() => Base64Fields.Decode("AAEC", maxBytes - 1));

        Assert.Equal("maxBytes", error.Keyword);
    }
}
