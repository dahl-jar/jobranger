using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;
using CvEntry = Jobranger.Cli.Features.Cv.Cv;

namespace Jobranger.Cli.Tests.Features.Cv;

[UnsupportedOSPlatform("windows")]
public sealed class CvTests : IDisposable {
    private const string markdownOnlyText = "shift-planning";
    private const int concurrentUploads = 2;
    private const int busyOperations = 1_000_000;

    private static readonly TimeSpan shortDeadline = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan workerDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan deadlineGrace = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan overlapWait = TimeSpan.FromMilliseconds(300);

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldThrowUnsupportedType_WhenSuffixIsDoc() {
        string path = temp.File("cv.doc");
        await File.WriteAllTextAsync(path, CvFixtures.LongLine, Token());
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.ExtractTextAsync(path, Token()));

        Assert.Equal(CvUnreadableException.UnsupportedType, reason);
    }

    [Fact]
    public async Task ShouldReadFile_WhenSuffixIsUpperCase() {
        string path = temp.File("CV.MD");
        File.Copy(CvFixtures.CvMarkdown, path);

        string text = await FakeCv().ExtractTextAsync(path, Token());

        Assert.Contains(markdownOnlyText, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrowUnsupportedType_WhenWebsiteUploadsDocx() {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(CvFixtures.Paragraph(CvFixtures.LongLine)));
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.TextFromUploadAsync("cv.docx", data, Token()));

        Assert.Equal(CvUnreadableException.UnsupportedType, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenFileIsMissing() {
        string path = temp.File("missing.pdf");
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.ExtractTextAsync(path, Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenFileDeniesReading() {
        Assert.SkipWhen(Environment.UserName == "root", "root ignores file modes");
        string path = temp.File("cv.md");
        File.Copy(CvFixtures.CvMarkdown, path);
        File.SetUnixFileMode(path, UnixFileMode.None);
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.ExtractTextAsync(path, Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenPathIsFifo() {
        string path = temp.File("cv.txt");
        using (Process mkfifo = Process.Start("/usr/bin/mkfifo", path)) {
            await mkfifo.WaitForExitAsync(Token());
        }
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.ExtractTextAsync(path, Token()).WaitAsync(deadlineGrace, Token()));

        Assert.Equal(CvUnreadableException.Unreadable, reason);
    }

    [Fact]
    public async Task ShouldReadTarget_WhenPathIsSymlink() {
        string path = temp.File("cv.md");
        File.CreateSymbolicLink(path, CvFixtures.CvMarkdown);

        string text = await FakeCv().ExtractTextAsync(path, Token());

        Assert.Contains(markdownOnlyText, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CvText.MaxBytes, null)]
    [InlineData(CvText.MaxBytes + 1, CvUnreadableException.TooLarge)]
    public async Task ShouldRejectFile_WhenOver5Mib(int size, string? expected) {
        string path = temp.File("cv.pdf");
        await File.WriteAllBytesAsync(path, new byte[size], Token());
        CvEntry cv = FakeCv();

        string? reason = await CvFixtures.ReasonAsync(() => cv.ExtractTextAsync(path, Token()));

        Assert.Equal(expected, reason);
    }

    [Fact]
    public async Task ShouldCleanWorkerText_WhenReplyHasControls() {
        CvEntry cv = FakeCv($"{{\"text\":\"{CvFixtures.ValidText}\\u0000\\u202E\"}}");

        string text = await cv.TextFromUploadAsync("cv.pdf", CvFixtures.PdfUpload, Token());

        Assert.Equal(CvFixtures.ValidText, text);
    }

    [Fact]
    public async Task ShouldRunOneWorkerAtATime_WhenUploadsOverlap() {
        int live = 0;
        int overlapped = 0;
        FakeChildProcessRunner runner = new(async _ => {
            if (Interlocked.Increment(ref live) > 1) {
                Interlocked.Exchange(ref overlapped, 1);
            }
            await Task.Delay(overlapWait, Token());
            Interlocked.Decrement(ref live);
            return new ChildExit(0, Encoding.UTF8.GetBytes(CvFixtures.ValidReply), TimedOut: false, OutputTooLarge: false);
        });
        CvEntry cv = new(Worker(runner, workerDeadline));

        await Task.WhenAll(Enumerable.Range(0, concurrentUploads)
            .Select(_ => cv.TextFromUploadAsync("cv.pdf", CvFixtures.PdfUpload, Token())));

        Assert.Equal(0, overlapped);
    }

    [Fact]
    public async Task ShouldExtractText_WhenPdfHasTextLayer() {
        string text = await RealCv(workerDeadline).ExtractTextAsync(CvFixtures.CvPdf, Token());

        Assert.Contains("java_project", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrowTooLarge_WhenHostileFilePassesDeadline() {
        byte[] busyPage = CvFixtures.FlateStream(Encoding.ASCII.GetBytes(
            CvFixtures.PdfTextLine + string.Concat(Enumerable.Repeat("0 0 Td\n", busyOperations))));
        byte[] data = CvFixtures.Pdf([.. Enumerable.Repeat(busyPage, PdfText.MaxPages)]);
        CvEntry cv = RealCv(shortDeadline);
        Stopwatch clock = Stopwatch.StartNew();

        string? reason = await CvFixtures.ReasonAsync(() => cv.TextFromUploadAsync("cv.pdf", data, Token()));

        Assert.True(reason == CvUnreadableException.TooLarge && clock.Elapsed < shortDeadline + deadlineGrace,
            $"reason {reason} after {clock.Elapsed}");
    }

    private static CancellationToken Token() => TestContext.Current.CancellationToken;

    private CvEntry FakeCv(string? reply = null) => new(Worker(new FakeChildProcessRunner(reply ?? CvFixtures.ValidReply), workerDeadline));

    private CvEntry RealCv(TimeSpan deadline) => new(CvFixtures.Runner(
        new ChildProcessRunner(), CvFixtures.Muxer, [CvFixtures.WorkerDll], temp.Path, deadline, Environment.GetEnvironmentVariables()));

    private CvWorkerRunner Worker(IChildProcessRunner runner, TimeSpan deadline) =>
        CvFixtures.Runner(runner, "/bin/sh", [], temp.Path, deadline);
}
