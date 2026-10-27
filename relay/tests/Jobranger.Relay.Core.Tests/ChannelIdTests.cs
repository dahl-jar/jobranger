using System.Buffers.Text;

namespace Jobranger.Relay.Core.Tests;

public sealed class ChannelIdTests {
    [Fact]
    public void ShouldDeriveChannelIdFromPrefixedPublicKey() {
        byte[] publicKey = Base64Url.DecodeFromChars(ProofVector.PublicKey);

        ChannelId channel = ChannelId.FromPublicKey(publicKey);

        Assert.Equal(new ChannelId(ProofVector.Channel), channel);
    }
}
