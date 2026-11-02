using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RunCommandSignalTests : IDisposable {
    private const string proxyVariable = "HTTP_PROXY";
    private const string deadProxy = "http://127.0.0.1:1";

    private static readonly TimeSpan signalLimit = TimeSpan.FromSeconds(2);

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public Task ShouldExit130_WhenCtrlCArrives() => AssertStopsAsync("-INT");

    [Fact]
    public Task ShouldExit130_WhenTerminated() => AssertStopsAsync("-TERM");

    private async Task AssertStopsAsync(string flag) {
        await using RawHttpServer server = new(string.Empty);
        Hashtable environment = RunCommandFixtures.Environment(temp, Locales.English);
        environment[proxyVariable] = deadProxy;
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: RunCommandFixtures.Ollama(server.Port)));
        RunCommandFixtures.SeedPendingAd(environment, TimeProvider.System);
        await using CliProcess cli = CliProcess.Start(environment, RunCommand.CommandName);
        await server.Request.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

        TimeSpan elapsed = await cli.SignalAsync(flag);

        Assert.Equal(
            (RunCommandFixtures.Interrupted, RunCommandFixtures.CancelledRow, string.Empty),
            (cli.ExitCode, string.Join(',', RunCommandFixtures.RunRows(environment)), cli.Output));
        Assert.InRange(elapsed, TimeSpan.Zero, signalLimit);
    }
}
