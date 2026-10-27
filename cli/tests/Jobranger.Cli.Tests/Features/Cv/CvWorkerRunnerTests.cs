using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;
using CvEntry = Jobranger.Cli.Features.Cv.Cv;

namespace Jobranger.Cli.Tests.Features.Cv;

[UnsupportedOSPlatform("windows")]
public sealed class CvWorkerRunnerTests : IDisposable {
    private const string shell = "/bin/sh";
    private const string runScript = "-c";
    private const string pdf = CvWorker.PdfSuffix;
    private const string systemRoot = "SYSTEMROOT";
    private const string heapLimit = "DOTNET_GCHeapHardLimit";
    private const string bundleDirectory = "DOTNET_BUNDLE_EXTRACT_BASE_DIR";
    private const int staleSeconds = 61;

    private static readonly TimeSpan shortDeadline = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan longDeadline = TimeSpan.FromSeconds(20);

    private readonly TempDir temp = new();

    public void Dispose() {
        foreach (string leftover in Directory.EnumerateDirectories(temp.Path, "*", SearchOption.AllDirectories)) {
            File.SetUnixFileMode(leftover, FileModes.OwnerOnlyDir);
        }
        temp.Dispose();
    }

    public static TheoryData<string> MalformedReplies => new() {
        "not json",
        "[]",
        "{\"text\": 5}",
        "{\"text\": null}",
        "{\"reason\": \"no_text\"}",
    };

    [Fact]
    public async Task ShouldReturnText_WhenWorkerReplies() {
        CvWorkerRunner worker = Shell("cat");

        string text = await worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token());

        Assert.Equal(CvFixtures.ValidText, text);
    }

    [Fact]
    public async Task ShouldThrowTooLarge_WhenWorkerMissesDeadline() {
        CvWorkerRunner worker = Shell("sleep 30", shortDeadline);

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token()));

        Assert.Equal(CvUnreadableException.TooLarge, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenWorkerCrashesAfterReply() {
        CvWorkerRunner worker = Shell("cat; exit 3");

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenWorkerCannotStart() {
        CvWorkerRunner worker = CvFixtures.Runner(new ChildProcessRunner(), temp.File("missing-worker"), [], temp.Path, longDeadline);

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenTempRootIsMissing() {
        CvWorkerRunner worker = CvFixtures.Runner(
            new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], temp.File("missing"), longDeadline);

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenTempRootIsReadOnly() {
        Assert.SkipWhen(Environment.UserName == "root", "root ignores directory modes");
        string readOnly = Directory.CreateDirectory(temp.File("read-only")).FullName;
        File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        CvWorkerRunner worker = CvFixtures.Runner(new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], readOnly, longDeadline);

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Theory]
    [MemberData(nameof(MalformedReplies))]
    public async Task ShouldThrowUnreadable_WhenReplyIsMalformed(string reply) {
        CvWorkerRunner worker = Shell("cat");

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(reply), Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowTooLarge_WhenWorkerReportsTooLarge() {
        CvWorkerRunner worker = Shell("cat");

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, "{\"reason\":\"too_large\"}"u8.ToArray(), Token()));

        Assert.Equal(CvUnreadableException.TooLarge, reason);
    }

    [Theory]
    [InlineData(CvFixtures.ReplyLimit, null)]
    [InlineData(CvFixtures.ReplyLimit + 1, CvUnreadableException.Unreadable)]
    public async Task ShouldLimitReplySize_WhenWorkerRepliesAtLimit(int size, string? expected) {
        CvWorkerRunner worker = Shell("cat");
        byte[] reply = ReplyOfSize(size);

        string? reason = await CvFixtures.ReasonAsync(() => worker.ExtractAsync(pdf, reply, Token()));

        Assert.Equal(expected, reason);
    }

    [Fact]
    public async Task ShouldDropOtherVariables_WhenStartingWorker() {
        FakeChildProcessRunner runner = new(CvFixtures.ValidReply);
        CvWorkerRunner worker = CvFixtures.Runner(runner, shell, [], temp.Path, longDeadline, new Hashtable {
            [systemRoot] = "C:/Windows",
            ["JOBRANGER_SENTINEL"] = "secret",
            ["HOME"] = temp.Path,
        });

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        IReadOnlyDictionary<string, string> environment = runner.Requests.Single().Environment;
        Assert.True(!environment.ContainsKey("HOME") && !environment.ContainsKey("JOBRANGER_SENTINEL") && environment.ContainsKey(systemRoot),
            string.Join(',', environment.Keys));
    }

    [Fact]
    public async Task ShouldCapWorkerHeap_WhenStartingWorker() {
        FakeChildProcessRunner runner = new(CvFixtures.ValidReply);
        CvWorkerRunner worker = CvFixtures.Runner(runner, shell, [], temp.Path, longDeadline);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.Equal("0x20000000", runner.Requests.Single().Environment[heapLimit]);
    }

    [Fact]
    public async Task ShouldPassRuntimeLocation_WhenDotnetVariablesAreSet() {
        FakeChildProcessRunner runner = new(CvFixtures.ValidReply);
        CvWorkerRunner worker = CvFixtures.Runner(runner, shell, [], temp.Path, longDeadline, new Hashtable {
            ["DOTNET_ROOT"] = "/opt/dotnet",
            [bundleDirectory] = "/opt/bundle",
            ["HOME"] = "/home/user",
        });

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        IReadOnlyDictionary<string, string> environment = runner.Requests.Single().Environment;
        Assert.Equal(["/opt/bundle", "/opt/dotnet"], [environment[bundleDirectory], environment["DOTNET_ROOT"]]);
    }

    [Fact]
    public async Task ShouldPassBundleDirectoryUnderHome_WhenExtractVariableIsUnset() {
        FakeChildProcessRunner runner = new(CvFixtures.ValidReply);
        CvWorkerRunner worker = CvFixtures.Runner(runner, shell, [], temp.Path, longDeadline, new Hashtable { ["HOME"] = "/home/user" });

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.Equal("/home/user/.net", runner.Requests.Single().Environment.GetValueOrDefault(bundleDirectory));
    }

    [Fact]
    public async Task ShouldStartCurrentProcess_WhenRunnerIsForCurrentProcess() {
        FakeChildProcessRunner runner = new(CvFixtures.ValidReply);
        CvWorkerRunner worker = CvWorkerRunner.ForCurrentProcess(runner, new Hashtable(), TimeProvider.System);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        ChildRequest request = runner.Requests.Single();
        Assert.Equal([Environment.ProcessPath, CvEntry.WorkerCommandName, pdf], [request.Program, .. request.Arguments]);
    }

    [Fact]
    public async Task ShouldRunWorkerInFreshDirectory_WhenStartingWorker() {
        CvWorkerRunner worker = Shell("printf '{\"text\":\"%s\"}' \"$(pwd)\"");

        string workdir = await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.StartsWith(CvWorkerRunner.WorkdirPrefix, Path.GetFileName(workdir), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldDenyWrites_WhenWorkerWritesInItsDirectory() {
        Assert.SkipWhen(Environment.UserName == "root", "root ignores directory modes");
        CvWorkerRunner worker = Shell("if mkdir leaked 2>/dev/null; then echo '{\"text\":\"written\"}'; else echo '{\"text\":\"denied\"}'; fi");

        string text = await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.Equal("denied", text);
    }

    [Fact]
    public async Task ShouldRemoveWorkdir_WhenWorkerFinishes() {
        CvWorkerRunner worker = Shell("cat");

        await worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token());

        Assert.Empty(Directory.EnumerateFileSystemEntries(CvFixtures.WorkdirParent(temp.Path)));
    }

    [Theory]
    [InlineData(staleSeconds, false)]
    [InlineData(0, true)]
    public async Task ShouldRemoveStaleWorkdir_WhenKilledRunLeftIt(int ageSeconds, bool kept) {
        string leftover = await StaleWorkdirAsync(CvFixtures.WorkdirParent(temp.Path), ageSeconds);
        CvWorkerRunner worker = CvFixtures.Runner(new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], temp.Path, longDeadline);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.Equal(kept, Directory.Exists(leftover));
    }

    [Fact]
    public async Task ShouldReturnText_WhenStaleWorkdirCannotBeRemoved() {
        Assert.SkipWhen(Environment.UserName == "root", "root ignores directory modes");
        string leftover = await StaleWorkdirAsync(CvFixtures.WorkdirParent(temp.Path), staleSeconds);
        File.SetUnixFileMode(leftover, FileModes.OwnerOnlyDir);
        await StaleWorkdirAsync(leftover, staleSeconds);
        File.SetUnixFileMode(leftover, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        Directory.SetLastWriteTimeUtc(leftover, DateTime.UtcNow.AddSeconds(-staleSeconds));
        CvWorkerRunner worker = Shell("cat");

        string text = await worker.ExtractAsync(pdf, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), Token());

        Assert.Equal(CvFixtures.ValidText, text);
    }

    [Fact]
    public async Task ShouldKeepLinkTargetMode_WhenStaleWorkdirIsSymlink() {
        string target = Directory.CreateDirectory(temp.File("target")).FullName;
        File.SetUnixFileMode(target, FileModes.SharedDir);
        string link = Path.Combine(Directory.CreateDirectory(CvFixtures.WorkdirParent(temp.Path)).FullName,
            CvWorkerRunner.WorkdirPrefix + "link");
        Directory.CreateSymbolicLink(link, target);
        Directory.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddSeconds(-staleSeconds));
        File.SetLastWriteTimeUtc(link, DateTime.UtcNow.AddSeconds(-staleSeconds));
        CvWorkerRunner worker = CvFixtures.Runner(new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], temp.Path, longDeadline);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.Equal(FileModes.SharedDir, File.GetUnixFileMode(target));
    }

    [Fact]
    public async Task ShouldSkipWorkdirParent_WhenOtherUserOwnsIt() {
        uint otherUser = UnixFiles.EffectiveUserId() + 1;
        string foreign = Directory.CreateDirectory(temp.File(CvWorkerRunner.WorkdirPrefix + otherUser)).FullName;
        string leftover = await StaleWorkdirAsync(foreign, staleSeconds);
        CvWorkerRunner worker = CvFixtures.Runner(
            new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], temp.Path, longDeadline, userId: otherUser);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.True(Directory.Exists(leftover), "stale workdir in a foreign parent was removed");
    }

    [Fact]
    public async Task ShouldSkipWorkdirParent_WhenParentIsSymlink() {
        string elsewhere = Directory.CreateDirectory(temp.File("elsewhere")).FullName;
        string leftover = await StaleWorkdirAsync(elsewhere, staleSeconds);
        Directory.CreateSymbolicLink(CvFixtures.WorkdirParent(temp.Path), elsewhere);
        CvWorkerRunner worker = CvFixtures.Runner(new FakeChildProcessRunner(CvFixtures.ValidReply), shell, [], temp.Path, longDeadline);

        await worker.ExtractAsync(pdf, ReadOnlyMemory<byte>.Empty, Token());

        Assert.True(Directory.Exists(leftover), "stale workdir behind a linked parent was removed");
    }

    private static CancellationToken Token() => TestContext.Current.CancellationToken;

    private static async Task<string> StaleWorkdirAsync(string parent, int ageSeconds) {
        string leftover = Directory.CreateDirectory(Path.Combine(parent, CvWorkerRunner.WorkdirPrefix + "killed")).FullName;
        await File.WriteAllTextAsync(Path.Combine(leftover, "leaked.bin"), "x", Token());
        File.SetUnixFileMode(leftover, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        Directory.SetLastWriteTimeUtc(leftover, DateTime.UtcNow.AddSeconds(-ageSeconds));
        return leftover;
    }

    private static byte[] ReplyOfSize(int size) {
        int padding = size - "{\"text\":\"\"}".Length - CvFixtures.LongLine.Length;
        return Encoding.UTF8.GetBytes($"{{\"text\":\"{CvFixtures.LongLine}{new string('x', padding)}\"}}");
    }

    private CvWorkerRunner Shell(string script, TimeSpan? deadline = null) =>
        CvFixtures.Runner(new ChildProcessRunner(), shell, [runScript, script], temp.Path, deadline ?? longDeadline);
}
