using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Features.Cv;

[UnsupportedOSPlatform("windows")]
internal sealed class CvWorkerRunner(
    IChildProcessRunner runner,
    string program,
    IReadOnlyList<string> leadingArguments,
    IDictionary environment,
    string tempRoot,
    uint userId,
    TimeSpan deadline,
    TimeProvider time) {
    public const string WorkdirPrefix = "jobranger-cv-";

    private const int replyLimit = 16 * CvText.MaxChars;
    private const string heapLimitVariable = "DOTNET_GCHeapHardLimit";
    private const string heapLimit = "0x20000000";
    private const string bundleDirectoryVariable = "DOTNET_BUNDLE_EXTRACT_BASE_DIR";
    private const string homeVariable = "HOME";
    private const string defaultBundleDirectory = ".net";
    private const string workdirPattern = WorkdirPrefix + "*";
    private const UnixFileMode workdirMode = UnixFileMode.UserRead | UnixFileMode.UserExecute;
    private const UnixFileMode ownerMode = workdirMode | UnixFileMode.UserWrite;

    private static readonly string[] keptVariables = ["SYSTEMROOT", "DOTNET_ROOT", bundleDirectoryVariable];
    private static readonly TimeSpan defaultDeadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan staleAge = TimeSpan.FromSeconds(60);

    public static CvWorkerRunner ForCurrentProcess(IChildProcessRunner runner, IDictionary environment, TimeProvider time) =>
        new(runner, Environment.ProcessPath ?? throw new InvalidOperationException("process path unknown, cv worker cannot start"),
            [], environment, Path.GetTempPath(), UnixFiles.EffectiveUserId(), defaultDeadline, time);

    public async Task<string> ExtractAsync(string suffix, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) {
        string? workdir = null;
        ChildExit exit;
        try {
            string parent = WorkdirParent();
            RemoveStaleWorkdirs(parent);
            workdir = Path.Combine(parent, WorkdirPrefix + Path.GetRandomFileName());
            Directory.CreateDirectory(workdir, workdirMode);
            ChildRequest request = new(
                program, [.. leadingArguments, Cv.WorkerCommandName, suffix], WorkerEnvironment(), workdir, data, replyLimit, deadline);
            exit = await runner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        } catch (Exception error) when (error is Win32Exception or IOException or UnauthorizedAccessException
            or InvalidOperationException) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable, error);
        } finally {
            if (workdir is not null) {
                RemoveWorkdir(workdir);
            }
        }
        if (exit.TimedOut) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
        if (exit.ExitCode != 0 || exit.OutputTooLarge) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable);
        }
        return TextFromReply(exit.Output);
    }

    private Dictionary<string, string> WorkerEnvironment() {
        Dictionary<string, string> kept = new(StringComparer.Ordinal) { [heapLimitVariable] = heapLimit };
        foreach (string name in keptVariables) {
            if (EnvironmentVariables.Read(environment, name) is string value) {
                kept[name] = value;
            }
        }
        if (!kept.ContainsKey(bundleDirectoryVariable) && EnvironmentVariables.Read(environment, homeVariable) is string home) {
            kept[bundleDirectoryVariable] = Path.Combine(home, defaultBundleDirectory);
        }
        return kept;
    }

    private static string TextFromReply(byte[] output) {
        try {
            using JsonDocument reply = JsonDocument.Parse(output);
            JsonElement root = reply.RootElement;
            if (root.TryGetProperty(CvWorker.TextKey, out JsonElement text)) {
                return text.GetString() ?? throw new CvUnreadableException(CvUnreadableException.Unreadable);
            }
            bool tooLarge = root.TryGetProperty(CvWorker.ReasonKey, out JsonElement reason)
                && reason.ValueEquals(CvUnreadableException.TooLarge);
            throw new CvUnreadableException(tooLarge ? CvUnreadableException.TooLarge : CvUnreadableException.Unreadable);
        } catch (Exception error) when (error is JsonException or InvalidOperationException) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable, error);
        }
    }

    private string WorkdirParent() {
        if (!Directory.Exists(tempRoot)) {
            throw new DirectoryNotFoundException($"cv temp root missing path={tempRoot}");
        }
        string shared = Path.Combine(tempRoot, WorkdirPrefix + userId.ToString(CultureInfo.InvariantCulture));
        if (!Path.Exists(shared) && new FileInfo(shared).LinkTarget is null) {
            Directory.CreateDirectory(shared, ownerMode);
        }
        if (UnixFiles.IsOwnDirectory(shared, userId)) {
            return shared;
        }
        return Directory.CreateDirectory($"{shared}-{Path.GetRandomFileName()}", ownerMode).FullName;
    }

    private void RemoveStaleWorkdirs(string parent) {
        DateTime cutoff = time.GetUtcNow().UtcDateTime - staleAge;
        foreach (string path in Directory.EnumerateDirectories(parent, workdirPattern)) {
            DirectoryInfo directory = new(path);
            if (directory.LinkTarget is null && directory.LastWriteTimeUtc < cutoff) {
                RemoveWorkdir(path);
            }
        }
    }

    private static void RemoveWorkdir(string path) {
        try {
            File.SetUnixFileMode(path, ownerMode);
            Directory.Delete(path, recursive: true);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return;
        }
    }
}
