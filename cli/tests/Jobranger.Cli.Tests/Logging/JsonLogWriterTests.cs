using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Tests.Logging;

[UnsupportedOSPlatform("windows")]
public sealed class JsonLogWriterTests : IDisposable {
    private const string fileName = "2026-09-16.jsonl";
    private const UnixFileMode sharedFile = FileModes.OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const int threadCount = 4;
    private const int linesPerThread = 2000;
    private const int testTimeoutMs = 5000;

    private static readonly DateOnly day = new(2026, 9, 16);
    private static readonly TimeSpan fifoLimit = TimeSpan.FromSeconds(2);

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldCreateLogWithMode0600_WhenLogDirIsNew() {
        string logDir = Path.Combine(temp.Path, "state", "logs");

        new JsonLogWriter(logDir).Write(Entry("run.started"), day);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(Path.Combine(logDir, fileName)));
    }

    [Fact]
    public void ShouldCreateLogDirWithMode0700_WhenLogDirIsNew() {
        string logDir = Path.Combine(temp.Path, "logs");

        new JsonLogWriter(logDir).Write(Entry("run.started"), day);

        Assert.Equal(FileModes.OwnerOnlyDir, File.GetUnixFileMode(logDir));
    }

    [Fact]
    public void ShouldTightenLogFile_WhenExistingFileIsShared() {
        string path = temp.File(fileName);
        File.WriteAllText(path, "");
        File.SetUnixFileMode(path, sharedFile);

        new JsonLogWriter(temp.Path).Write(Entry("run.started"), day);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldKeepEarlierLines_WhenFileExists() {
        JsonLogWriter writer = new(temp.Path);
        writer.Write(Entry("run.started"), day);

        writer.Write(Entry("run.finished"), day);

        Assert.Equal(
            ["run.started", "run.finished"],
            LogLines.Read(temp.File(fileName)).Select(entry => (string?)entry["event"]));
    }

    [Fact]
    public void ShouldDropLine_WhenLogDirIsAFile() {
        string logDir = temp.File("logs");
        File.WriteAllText(logDir, "");

        new JsonLogWriter(logDir).Write(Entry("run.started"), day);

        Assert.Equal("", File.ReadAllText(logDir));
    }

    [Fact]
    public async Task ShouldKeepEveryLine_WhenTwoWritersAppendAtOnce() {
        JsonLogWriter[] writers = [new(temp.Path), new(temp.Path)];

        await Task.WhenAll(Enumerable.Range(0, threadCount).Select(thread => Task.Factory.StartNew(
            () => WriteLines(writers[thread % writers.Length], thread),
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)));

        Assert.Equal(
            threadCount * linesPerThread,
            LogLines.Read(temp.File(fileName)).Select(entry => (string?)entry["event"]).Distinct().Count());
    }

    [Fact]
    public void ShouldLeaveLinkTargetUntouched_WhenDayFileIsSymlink() {
        string target = temp.File("target.txt");
        File.WriteAllText(target, "kept");
        File.SetUnixFileMode(target, sharedFile);
        File.CreateSymbolicLink(temp.File(fileName), target);

        new JsonLogWriter(temp.Path).Write(Entry("run.started"), day);

        Assert.Equal(("kept", sharedFile), (File.ReadAllText(target), File.GetUnixFileMode(target)));
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldReturnWithoutWriting_WhenDayFileIsFifo() {
        MakeFifo(temp.File(fileName));
        JsonLogWriter writer = new(temp.Path);

        Task write = Task.Factory.StartNew(
            () => writer.Write(Entry("run.started"), day),
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Assert.Same(write, await Task.WhenAny(write, Task.Delay(fifoLimit, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void ShouldDropLine_WhenDayFileIsFifoWithReader() {
        string path = temp.File(fileName);
        MakeFifo(path);
        using FileStream reader = new(UnixFiles.OpenReadNoFollow(path, out _)!, FileAccess.Read, bufferSize: 0);

        new JsonLogWriter(temp.Path).Write(Entry("run.started"), day);

        using (SafeFileHandle marker = UnixFiles.OpenAppendNoFollow(path, out _)!) {
            UnixFiles.Write(marker, "end\n"u8);
        }
        byte[] buffer = new byte[256];
        Assert.Equal("end\n", Encoding.UTF8.GetString(buffer, 0, reader.Read(buffer)));
    }

    private static void MakeFifo(string path) {
        using Process process = Process.Start("mkfifo", [path]);
        process.WaitForExit();
    }

    private static void WriteLines(JsonLogWriter writer, int thread) {
        for (int line = 0; line < linesPerThread; line++) {
            writer.Write(Entry($"run.t{thread}_{line}"), day);
        }
    }

    private static JsonObject Entry(string name) => new() { ["event"] = name };
}
