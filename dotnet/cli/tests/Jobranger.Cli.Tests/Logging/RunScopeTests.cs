using Jobranger.Cli.Logging;

namespace Jobranger.Cli.Tests.Logging;

public sealed class RunScopeTests {
    [Fact]
    public void ShouldRestorePreviousRunId_WhenInnerScopeIsDisposed() {
        using IDisposable outer = RunScope.Bind("run-1");

        RunScope.Bind("run-2").Dispose();

        Assert.Equal("run-1", RunScope.Current);
    }
}
