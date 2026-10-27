using Jobranger.Cli.Features.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class FrameExceptionTests {
    [Fact]
    public void ShouldNameReasonOnly_WhenFrameIsRejected() {
        FrameException exception = new(FrameFailure.Tag);

        Assert.Equal("frame rejected reason=Tag", exception.Message);
    }
}
