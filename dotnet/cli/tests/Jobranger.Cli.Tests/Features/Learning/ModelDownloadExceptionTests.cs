using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class ModelDownloadExceptionTests {
    [Fact]
    public void ShouldNameFileAndIdleTime_WhenBodyStalls() {
        ModelDownloadException exception = new("model.onnx", TimeSpan.FromSeconds(30));

        Assert.Equal("model.onnx download stalled, no bytes for 30 s", exception.Message);
    }
}
