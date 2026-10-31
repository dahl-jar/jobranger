using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RunCommandSignalTests : IDisposable {
    private const int interrupted = 130;
    private const string cancelledRow = "cancelled|1";

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
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: RunCommandFixtures.Ollama(server.Port)));
        using (SqliteConnection database = Database.Open(new Paths(environment).DbPath(), NullLogger.Instance)) {
            new AdsEntry(database, TimeProvider.System).Upsert(RunFixtures.Pending(1, "Utvikler", "Org One AS"));
        }
        await using CliProcess cli = CliProcess.Start(environment, RunCommand.CommandName);
        await server.Request.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

        TimeSpan elapsed = await cli.SignalAsync(flag);

        Assert.Equal(
            (interrupted, cancelledRow, string.Empty),
            (cli.ExitCode, string.Join(',', RunCommandFixtures.RunRows(environment)), cli.Output));
        Assert.InRange(elapsed, TimeSpan.Zero, signalLimit);
    }
}
