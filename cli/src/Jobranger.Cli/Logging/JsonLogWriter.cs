using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Logging;

[UnsupportedOSPlatform("windows")]
internal sealed class JsonLogWriter(string logDir) {
    public const string FileSuffix = ".jsonl";
    public const string DayFormat = "yyyy-MM-dd";

    private const char lineEnd = '\n';

    private readonly Lock gate = new();
    private string? privatePath;

    public void Write(JsonObject entry, DateOnly day) {
        byte[] line = Encoding.UTF8.GetBytes(entry.ToJsonString() + lineEnd);
        string path = Path.Combine(logDir, day.ToString(DayFormat, CultureInfo.InvariantCulture) + FileSuffix);
        lock (gate) {
            try {
                Append(path, line);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                return;
            }
        }
    }

    private static SafeFileHandle? OpenDayFile(string path) {
        SafeFileHandle? handle = UnixFiles.OpenAppendNoFollow(path, out int error);
        if (handle is not null || !UnixFiles.IsMissing(error)) {
            return handle;
        }
        CreateEmpty(path);
        return UnixFiles.OpenAppendNoFollow(path, out _);
    }

    private static void CreateEmpty(string path) {
        try {
            using FileStream created = new(path, new FileStreamOptions {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = PrivateFile.OwnerOnlyFileMode,
            });
        } catch (IOException error) when (UnixFiles.IsExisting(error.HResult)) {
            return;
        }
    }

    private void Append(string path, byte[] line) {
        bool firstWrite = path != privatePath;
        if (firstWrite) {
            PrivateFile.CreateParentDir(path, NullLogger.Instance);
        }
        using SafeFileHandle? handle = OpenDayFile(path);
        if (handle is null || !UnixFiles.IsRegularFile(handle)) {
            return;
        }
        if (firstWrite) {
            File.SetUnixFileMode(handle, PrivateFile.OwnerOnlyFileMode);
            privatePath = path;
        }
        UnixFiles.Write(handle, line);
    }
}
