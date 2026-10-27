using Jobranger.Cli.Features.Runs;

namespace Jobranger.Cli.Tests.Features.Runs;

public sealed class RunStagesTests {
    [Theory]
    [InlineData("SourceStarted", "source_started")]
    [InlineData("SourceDone", "source_done")]
    [InlineData("SourceFailed", "source_failed")]
    [InlineData("Model", "model")]
    [InlineData("ApplyChecks", "apply_checks")]
    [InlineData("Report", "report")]
    public void ShouldUseWireName_WhenStageIsNamed(string stage, string expected) {
        string name = RunStages.WireName(Enum.Parse<RunStage>(stage));

        Assert.Equal(expected, name);
    }
}
