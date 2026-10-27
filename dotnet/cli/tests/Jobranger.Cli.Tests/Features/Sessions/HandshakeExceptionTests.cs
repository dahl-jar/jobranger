using Jobranger.Cli.Features.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class HandshakeExceptionTests {
    [Fact]
    public void ShouldNameReasonOnly_WhenHandshakeFails() {
        HandshakeException exception = new(HandshakeFailure.Mac);

        Assert.Equal("handshake aborted reason=Mac", exception.Message);
    }
}
