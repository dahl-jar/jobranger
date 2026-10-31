using Jobranger.Messaging.Channels;
using SessionsEntry = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class SessionsTests {
    private static readonly ChannelId channel = new(SessionVector.Text("channel"));

    [Fact]
    public void ShouldReproducePairTag_WhenCodeComesFromSessionV1() {
        byte[] tag = SessionsEntry.PairTag(SessionVector.Text("pair_code"), channel);

        Assert.Equal(SessionVector.Bytes("pair_tag"), tag);
    }

    [Fact]
    public void ShouldMatchPairTag_WhenCodeIsTheShownOne() {
        bool matches = SessionsEntry.IsPairTag(SessionVector.Text("pair_code"), channel, SessionVector.Bytes("pair_tag"));

        Assert.True(matches);
    }

    [Fact]
    public void ShouldRejectPairTag_WhenCodeDiffers() {
        bool matches = SessionsEntry.IsPairTag("AAAA-BBBB", channel, SessionVector.Bytes("pair_tag"));

        Assert.False(matches);
    }
}
