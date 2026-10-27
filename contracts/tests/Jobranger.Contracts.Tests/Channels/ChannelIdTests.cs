using System.Buffers.Text;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Tests.Proofs;

namespace Jobranger.Contracts.Tests.Channels;

public sealed class ChannelIdTests {
    [Fact]
    public void ShouldDeriveChannelIdFromPrefixedPublicKey() {
        byte[] publicKey = Base64Url.DecodeFromChars(ProofVector.PublicKey);

        ChannelId channel = ChannelId.FromPublicKey(publicKey);

        Assert.Equal(new ChannelId(ProofVector.Channel), channel);
    }
}
