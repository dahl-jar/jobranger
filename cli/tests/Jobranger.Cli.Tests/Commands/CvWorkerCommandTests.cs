using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class CvWorkerCommandTests : IDisposable {
    private const string workerCommand = "cv-worker";
    private const string oldLog = "2020-01-01.jsonl";
    private const int fileSizeSignalExit = 153;

    private static readonly TimeSpan deadline = TimeSpan.FromSeconds(30);

    private readonly ChildProcessRunner runner = new();
    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldReturnUsage_WhenWorkerArgumentsHaveSeparator() {
        ChildRequest request = Worker(CvFixtures.Muxer, [CvFixtures.WorkerDll, workerCommand, "--", CvWorker.PdfSuffix],
            new Dictionary<string, string> { ["HOME"] = temp.Path });

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, exit.ExitCode);
    }

    [Fact]
    public async Task ShouldBlockWrite_WhenWorkerWritesFile() {
        string reply = temp.File("reply.json");
        ChildRequest request = Worker("/bin/sh",
            ["-c", "exec \"$0\" \"$@\" > reply.json", CvFixtures.Muxer, CvFixtures.WorkerDll, workerCommand, CvWorker.PdfSuffix],
            new Dictionary<string, string>());

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal((fileSizeSignalExit, 0L), (exit.ExitCode, new FileInfo(reply).Length));
    }

    [Fact]
    public async Task ShouldLeaveLogsAlone_WhenWorkerStarts() {
        string logs = Directory.CreateDirectory(Path.Combine(temp.Path, "state", "logs")).FullName;
        await File.WriteAllTextAsync(Path.Combine(logs, oldLog), "{}", TestContext.Current.CancellationToken);
        ChildRequest request = Worker(CvFixtures.Muxer, [CvFixtures.WorkerDll, workerCommand, CvWorker.PdfSuffix],
            new Dictionary<string, string> {
                ["HOME"] = temp.Path,
                ["JOBRANGER_STATE_DIR"] = Path.Combine(temp.Path, "state"),
            });

        ChildExit exit = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(exit.ExitCode == 0 && Encoding.UTF8.GetString(exit.Output).StartsWith("{\"text\"", StringComparison.Ordinal)
            && File.Exists(Path.Combine(logs, oldLog)), $"exit {exit.ExitCode}, old log kept {File.Exists(Path.Combine(logs, oldLog))}");
    }

    private ChildRequest Worker(string program, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment) =>
        new(program, arguments, environment, temp.Path, File.ReadAllBytes(CvFixtures.CvPdf), CvFixtures.ReplyLimit, deadline);
}
