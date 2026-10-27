using System.Collections.Frozen;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Features.Cv;

internal sealed class Cv {
    public const string WorkerCommandName = "cv-worker";

    private static readonly FrozenSet<string> textSuffixes = FrozenSet.Create(StringComparer.Ordinal, ".txt", ".md");
    private static readonly FrozenSet<string> readSuffixes =
        FrozenSet.Create(StringComparer.Ordinal, [.. textSuffixes, CvWorker.PdfSuffix, CvWorker.DocxSuffix]);
    private static readonly FrozenSet<string> uploadSuffixes =
        FrozenSet.Create(StringComparer.Ordinal, [.. textSuffixes, CvWorker.PdfSuffix]);
    private static readonly SemaphoreSlim extractLock = new(1, 1);

    private readonly CvWorkerRunner worker;

    [UnsupportedOSPlatform("windows")]
    public Cv(CvWorkerRunner worker) {
        this.worker = worker;
    }

    [UnsupportedOSPlatform("windows")]
    public async Task<string> ExtractTextAsync(string path, CancellationToken cancellationToken) {
        string suffix = Path.GetExtension(path).ToLowerInvariant();
        if (!readSuffixes.Contains(suffix)) {
            throw new CvUnreadableException(CvUnreadableException.UnsupportedType);
        }
        return await TextFromBytesAsync(suffix, ReadRegularFile(path), cancellationToken).ConfigureAwait(false);
    }

    [UnsupportedOSPlatform("windows")]
    public async Task<string> TextFromUploadAsync(string name, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) {
        string suffix = Path.GetExtension(name).ToLowerInvariant();
        if (!uploadSuffixes.Contains(suffix)) {
            throw new CvUnreadableException(CvUnreadableException.UnsupportedType);
        }
        return await TextFromBytesAsync(suffix, data, cancellationToken).ConfigureAwait(false);
    }

    public static Task RunWorkerAsync(string suffix, Stream input, Stream output) => CvWorker.RunAsync(suffix, input, output);

    [UnsupportedOSPlatform("windows")]
    private async Task<string> TextFromBytesAsync(string suffix, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) {
        if (data.Length > CvText.MaxBytes) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
        if (textSuffixes.Contains(suffix)) {
            return CvText.FromTextFile(data.ToArray());
        }
        await extractLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return CvText.Checked(await worker.ExtractAsync(suffix, data, cancellationToken).ConfigureAwait(false));
        } finally {
            extractLock.Release();
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static byte[] ReadRegularFile(string path) {
        try {
            string target = File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
            using SafeFileHandle handle = UnixFiles.OpenReadNoFollow(target, out _)
                ?? throw new CvUnreadableException(CvUnreadableException.Unreadable);
            if (!UnixFiles.IsRegularFile(handle)) {
                throw new CvUnreadableException(CvUnreadableException.Unreadable);
            }
            using FileStream stream = new(handle, FileAccess.Read);
            byte[] buffer = new byte[CvText.MaxBytes + 1];
            int total = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return buffer[..total];
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable, error);
        }
    }
}
