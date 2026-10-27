using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class ModelHashExceptionTests {
    [Fact]
    public void ShouldNameFileAndHashes_WhenHashDiffers() {
        ModelHashException exception = new("model.onnx", "aa", "bb");

        Assert.Equal("model.onnx sha256 bb differs from pinned aa, file deleted", exception.Message);
    }

    [Fact]
    public void ShouldSayFileWasKept_WhenDeleteFailed() {
        ModelHashException exception = new("model.onnx", "aa", "bb", new UnauthorizedAccessException());

        Assert.Equal("model.onnx sha256 bb differs from pinned aa, file could not be deleted", exception.Message);
    }
}
