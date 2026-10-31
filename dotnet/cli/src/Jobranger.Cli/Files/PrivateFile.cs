using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Files;

[UnsupportedOSPlatform("windows")]
internal static partial class PrivateFile {
    public const UnixFileMode OwnerOnlyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode ownerOnlyDirMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const string tempSuffix = ".tmp";
    private const string tempSeparator = ".";
    private const string anyName = "*";
    private const string symlinkReason = "symbolic link";
    private const string notRegularReason = "not a regular file";
    private const string openFailedReason = "open failed errno=";
    private const UnixFileMode openToOthers =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static void Write(string path, ReadOnlySpan<byte> content, ILogger logger) =>
        Replace(path, CreateParentDir(path, logger), content);

    public static void WriteThroughLink(string path, ReadOnlySpan<byte> content, ILogger logger) {
        FileInfo file = new(path);
        if (file.LinkTarget is null) {
            Write(path, content, logger);
        } else {
            string target = file.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
            Replace(target, DirOf(target), content);
        }
    }

    public static string CreateParentDir(string filePath, ILogger logger) {
        string dir = DirOf(filePath);
        try {
            if (Directory.Exists(dir)) {
                TightenDir(dir, logger);
            } else {
                CreatePrivateDir(dir);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new PrivateDirException(dir, error);
        }
        return dir;
    }

    public static void RemoveStaleTemps(string path) {
        string dir = DirOf(path);
        if (!Directory.Exists(dir)) {
            return;
        }
        string pattern = TempPrefix(path) + anyName + tempSuffix;
        foreach (string stale in Directory.EnumerateFiles(dir, pattern)) {
            File.Delete(stale);
        }
    }

    public static FileStream? OpenRead(string path, ILogger logger) {
        SafeFileHandle? opened = UnixFiles.OpenReadNoFollow(path, out int error);
        if (opened is null) {
            return UnixFiles.IsMissing(error) ? null : throw OpenError(path, error);
        }
        try {
            if (!UnixFiles.IsRegularFile(opened)) {
                throw new PrivateFileException(path, notRegularReason);
            }
            if ((File.GetUnixFileMode(opened) & openToOthers) != 0) {
                File.SetUnixFileMode(opened, OwnerOnlyFileMode);
                LogFileTightened(logger, Path.GetFileName(path));
            }
            return new FileStream(opened, FileAccess.Read);
        } catch {
            opened.Dispose();
            throw;
        }
    }

    public static PrivateFileException OpenError(string path, int error) =>
        new(path, UnixFiles.IsLink(error) ? symlinkReason : openFailedReason + error);

    private static void Replace(string path, string dir, ReadOnlySpan<byte> content) {
        string temp = Path.Combine(dir, TempPrefix(path) + Path.GetRandomFileName() + tempSuffix);
        try {
            using (FileStream stream = new(temp, new FileStreamOptions {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = OwnerOnlyFileMode,
            })) {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        } catch {
            File.Delete(temp);
            throw;
        }
    }

    private static void CreatePrivateDir(string dir) {
        Stack<string> missing = [];
        string? current = dir;
        while (current is not null && !Directory.Exists(current)) {
            missing.Push(current);
            current = Path.GetDirectoryName(current);
        }
        foreach (string created in missing) {
            Directory.CreateDirectory(created, ownerOnlyDirMode);
        }
    }

    private static void TightenDir(string dir, ILogger logger) {
        if ((File.GetUnixFileMode(dir) & openToOthers) == 0) {
            return;
        }
        File.SetUnixFileMode(dir, ownerOnlyDirMode);
        LogDirTightened(logger, Path.GetFileName(dir));
    }

    private static string DirOf(string path) => Path.GetDirectoryName(Path.GetFullPath(path))!;

    private static string TempPrefix(string path) => tempSeparator + Path.GetFileName(path) + tempSeparator;

    [LoggerMessage(
        EventName = "paths.private_file.tightened",
        Level = LogLevel.Warning,
        Message = "tightened file mode to 0600 file={File}")]
    private static partial void LogFileTightened(ILogger logger, string file);

    [LoggerMessage(
        EventName = "paths.private_dir.tightened",
        Level = LogLevel.Warning,
        Message = "tightened dir mode to 0700 dir={Dir}")]
    private static partial void LogDirTightened(ILogger logger, string dir);
}
