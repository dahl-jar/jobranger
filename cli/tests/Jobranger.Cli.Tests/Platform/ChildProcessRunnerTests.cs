using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Platform;

[UnsupportedOSPlatform("windows")]
public sealed class ChildProcessRunnerTests : IDisposable {
    private const string shell = "/bin/sh";
    private const string runScript = "-c";
    private const string scriptName = "child";
    private const int outputLimit = 1000;

    private static readonly TimeSpan shortDeadline = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan longDeadline = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan exitGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ChildProcessRunner runner = new();
    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldPassOnlyGivenVariables_WhenStartingChild() {
        ChildRequest request = Request("/usr/bin/env", []) with {
            Environment = new Dictionary<string, string> { ["KEPT"] = "1" },
        };

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("KEPT=1\n", Encoding.UTF8.GetString(exit.Output));
    }

    [Fact]
    public async Task ShouldPassInput_WhenChildReadsStdin() {
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(CvFixtures.LongLine, 2000)));
        ChildRequest request = Request("/bin/cat", []) with { Input = input, OutputLimit = input.Length };

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(input, exit.Output);
    }

    [Fact]
    public async Task ShouldRunInGivenDirectory_WhenStartingChild() {
        ChildRequest request = Request("/bin/pwd", ["-P"]);

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.EndsWith(Path.DirectorySeparatorChar + Path.GetFileName(temp.Path) + "\n", Encoding.UTF8.GetString(exit.Output),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnExitCode_WhenChildFails() {
        ChildRequest request = Script("exit 3");

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(3, exit.ExitCode);
    }

    [Fact]
    public async Task ShouldWaitForExit_WhenChildClosesOutputFirst() {
        ChildRequest request = Script("exec >&-; sleep 0.3; exit 3");

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(3, exit.ExitCode);
    }

    [Fact]
    public async Task ShouldReturnExitCode_WhenChildIgnoresLargeInput() {
        ChildRequest request = Script("exit 0") with { Input = new byte[CvText.MaxBytes] };

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit.ExitCode);
    }

    [Fact]
    public async Task ShouldReturnTimedOut_WhenChildPassesDeadline() {
        ChildRequest request = Script("sleep 30") with { Deadline = shortDeadline };
        Stopwatch clock = Stopwatch.StartNew();

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(exit.TimedOut && !exit.OutputTooLarge && clock.Elapsed < exitGrace,
            $"timed out {exit.TimedOut}, too large {exit.OutputTooLarge} after {clock.Elapsed}");
    }

    [Fact]
    public async Task ShouldKillGrandchild_WhenDeadlinePasses() {
        ChildRequest request = Script("sleep 30 & echo $! > pid; wait") with { Deadline = shortDeadline };

        await runner.RunAsync(request, TestContext.Current.CancellationToken);

        int grandchild = int.Parse(File.ReadAllText(temp.File("pid")).Trim(), CultureInfo.InvariantCulture);
        Assert.True(await HasEndedAsync(grandchild), $"process {grandchild} still running");
    }

    [Fact]
    public async Task ShouldThrowCancelled_WhenCallerCancels() {
        ChildRequest request = Script("sleep 30");
        using CancellationTokenSource cancel = new(shortDeadline);
        Stopwatch clock = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(request, cancel.Token));

        Assert.True(clock.Elapsed < exitGrace, $"cancel took {clock.Elapsed}");
    }

    [Theory]
    [InlineData(outputLimit, false)]
    [InlineData(outputLimit + 1, true)]
    public async Task ShouldReportOutputTooLarge_WhenChildPassesOutputLimit(int size, bool tooLarge) {
        ChildRequest request = Script("head -c \"$1\" /dev/zero", size.ToString(CultureInfo.InvariantCulture));

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(tooLarge, exit.OutputTooLarge);
    }

    [Fact]
    public async Task ShouldStopChild_WhenOutputPassesLimit() {
        ChildRequest request = Script("cat /dev/zero");
        Stopwatch clock = Stopwatch.StartNew();

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(exit.OutputTooLarge && !exit.TimedOut && clock.Elapsed < exitGrace,
            $"too large {exit.OutputTooLarge}, timed out {exit.TimedOut} after {clock.Elapsed}");
    }

    [Fact]
    public async Task ShouldFinish_WhenChildFloodsStderr() {
        ChildRequest request = Script("head -c 1000000 /dev/zero >&2; echo done") with { Deadline = longDeadline };

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("done\n", Encoding.UTF8.GetString(exit.Output));
    }

    private ChildRequest Request(string program, IReadOnlyList<string> arguments) => new(
        program, arguments, new Dictionary<string, string>(), temp.Path, ReadOnlyMemory<byte>.Empty, outputLimit, longDeadline);

    private ChildRequest Script(string script, params string[] arguments) =>
        Request(shell, [runScript, script, scriptName, .. arguments]);

    private static async Task<bool> HasEndedAsync(int processId) {
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < exitGrace) {
            try {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited) {
                    return true;
                }
            } catch (ArgumentException) {
                return true;
            }
            await Task.Delay(pollInterval, TestContext.Current.CancellationToken);
        }
        return false;
    }
}
