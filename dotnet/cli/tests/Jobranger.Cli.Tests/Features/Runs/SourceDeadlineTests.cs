using Jobranger.Cli.Features.Runs;

namespace Jobranger.Cli.Tests.Features.Runs;

public sealed class SourceDeadlineTests {
    [Theory]
    [InlineData("finn", 40)]
    [InlineData("nav", 20)]
    public void ShouldReturnSourceDeadline_WhenSourceIsNamed(string source, int minutes) {
        TimeSpan deadline = SourceDeadline.For(source);

        Assert.Equal(TimeSpan.FromMinutes(minutes), deadline);
    }
}
