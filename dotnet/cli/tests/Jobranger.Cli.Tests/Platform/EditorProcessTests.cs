using System.Diagnostics;
using System.Globalization;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Platform;

public sealed class EditorProcessTests : IDisposable {
    private const string shell = "/bin/sh";
    private const string runScript = "-c";
    private const string failedExitScript = "exit 3";
    private const int failedExit = 3;

    private readonly TempDir temp = new();
    private readonly EditorProcess process = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldReturnExitCode_WhenProgramExits() {
        int exit = await process.RunAsync(shell, [runScript, failedExitScript], TestContext.Current.CancellationToken);

        Assert.Equal(failedExit, exit);
    }

    [Fact]
    public async Task ShouldStopProgram_WhenCancelled() {
        string processFile = temp.File("pid");
        using CancellationTokenSource cancelled = new();
        Task<int> running = process.RunAsync(shell, [runScript, $"echo $$ > {processFile}; exec sleep 30"], cancelled.Token);
        await TestWaits.UntilAsync(() => File.Exists(processFile) && File.ReadAllText(processFile).EndsWith('\n'));
        int processId = int.Parse(File.ReadAllText(processFile), CultureInfo.InvariantCulture);

        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await TestWaits.UntilAsync(() => IsGone(processId));
    }

    private static bool IsGone(int processId) {
        try {
            using Process found = Process.GetProcessById(processId);
            return found.HasExited;
        } catch (ArgumentException) {
            return true;
        }
    }
}
