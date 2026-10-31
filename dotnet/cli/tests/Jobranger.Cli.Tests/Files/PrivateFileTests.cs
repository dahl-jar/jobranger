using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Files;

[UnsupportedOSPlatform("windows")]
public sealed class PrivateFileTests : IDisposable {
    private const UnixFileMode groupReadable = FileModes.OwnerReadWrite | UnixFileMode.GroupRead;

    private static readonly byte[] content = Encoding.UTF8.GetBytes("{\"key\":\"value\"}");

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldCreateFileWithMode0600_WhenWritingPrivateFile() {
        string path = temp.File("secrets.json");

        PrivateFile.Write(path, content, NullLogger.Instance);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldCreateParentDirsWithMode0700_WhenTheyAreMissing() {
        string path = Path.Combine(temp.Path, "nested", "jobranger", "secrets.json");

        PrivateFile.Write(path, content, NullLogger.Instance);

        UnixFileMode[] modes = [
            File.GetUnixFileMode(Path.Combine(temp.Path, "nested")),
            File.GetUnixFileMode(Path.GetDirectoryName(path)!),
        ];
        Assert.Equal([FileModes.OwnerOnlyDir, FileModes.OwnerOnlyDir], modes);
    }

    [Fact]
    public void ShouldTightenDirTo0700_WhenItIsOpenToOthers() {
        string dir = Path.Combine(temp.Path, "jobranger");
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, FileModes.SharedDir);

        PrivateFile.Write(Path.Combine(dir, "config.yaml"), content, NullLogger.Instance);

        Assert.Equal(FileModes.OwnerOnlyDir, File.GetUnixFileMode(dir));
    }

    [Fact]
    public void ShouldWarnWithDirNameOnly_WhenTighteningDir() {
        string dir = Path.Combine(temp.Path, "jobranger");
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, FileModes.SharedDir);
        FakeLogger logger = new();

        PrivateFile.Write(Path.Combine(dir, "config.yaml"), content, logger);

        Assert.Equal(
            ["paths.private_dir.tightened tightened dir mode to 0700 dir=jobranger"],
            logger.Collector.GetSnapshot().Select(record => $"{record.Id.Name} {record.Message}"));
    }

    [Fact]
    public void ShouldRemoveTempFile_WhenReplaceFails() {
        string path = temp.File("config.yaml");
        Directory.CreateDirectory(Path.Combine(path, "occupied"));

        Assert.ThrowsAny<IOException>(() => PrivateFile.Write(path, content, NullLogger.Instance));

        Assert.Equal(["config.yaml"], Directory.GetFileSystemEntries(temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void ShouldRemoveOnlyItsOwnTemps_WhenRemovingStaleTemps() {
        string path = temp.File("secrets.json");
        File.WriteAllText(temp.File(".secrets.json.a1b2c3.tmp"), "partial");
        File.WriteAllText(temp.File(".link.json.a1b2c3.tmp"), "partial");

        PrivateFile.RemoveStaleTemps(path);

        Assert.Equal([".link.json.a1b2c3.tmp"], Directory.GetFileSystemEntries(temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void ShouldSkipRemoval_WhenDirIsMissing() {
        string path = Path.Combine(temp.Path, "absent", "secrets.json");

        Exception? error = Record.Exception(() => PrivateFile.RemoveStaleTemps(path));

        Assert.Null(error);
    }

    [Fact]
    public void ShouldReturnContent_WhenFileIsPrivate() {
        string path = temp.File("secrets.json");
        PrivateFile.Write(path, content, NullLogger.Instance);

        using FileStream? stream = PrivateFile.OpenRead(path, NullLogger.Instance);

        Assert.Equal(content, ReadAll(stream!));
    }

    [Fact]
    public void ShouldNotWarn_WhenFileIsAlreadyPrivate() {
        string path = temp.File("secrets.json");
        PrivateFile.Write(path, content, NullLogger.Instance);
        FakeLogger logger = new();

        using (PrivateFile.OpenRead(path, logger)) {
        }

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public void ShouldNotWarn_WhenDirIsAlreadyPrivate() {
        FakeLogger logger = new();

        PrivateFile.Write(temp.File("config.yaml"), content, logger);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Theory]
    [InlineData("/dev/null")]
    [InlineData("/")]
    public void ShouldRefuseFile_WhenPathIsNotRegularFile(string path) {
        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => PrivateFile.OpenRead(path, NullLogger.Instance));

        Assert.Equal("not a regular file", refused.Reason);
    }

    [Fact]
    public void ShouldReturnNull_WhenFileIsMissing() {
        using FileStream? stream = PrivateFile.OpenRead(temp.File("absent.json"), NullLogger.Instance);

        Assert.Null(stream);
    }

    [Fact]
    public void ShouldRefuseFile_WhenPathIsSymlink() {
        string target = temp.File("target.json");
        PrivateFile.Write(target, content, NullLogger.Instance);
        string link = temp.File("secrets.json");
        File.CreateSymbolicLink(link, target);

        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => PrivateFile.OpenRead(link, NullLogger.Instance));

        Assert.Equal("symbolic link", refused.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task ShouldRefuseFile_WhenPathIsFifo() {
        string path = temp.File("secrets.json");
        MakeFifo(path);

        PrivateFileException refused = await Assert.ThrowsAsync<PrivateFileException>(() => Task.Run(
            () => PrivateFile.OpenRead(path, NullLogger.Instance), TestContext.Current.CancellationToken));

        Assert.Equal("not a regular file", refused.Reason);
    }

    [Fact]
    public void ShouldRefuseFile_WhenOwnerCannotRead() {
        string path = temp.File("secrets.json");
        PrivateFile.Write(path, content, NullLogger.Instance);
        File.SetUnixFileMode(path, UnixFileMode.None);

        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => PrivateFile.OpenRead(path, NullLogger.Instance));

        Assert.StartsWith("open failed", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldTightenModeTo0600_WhenFileIsOpenToGroup() {
        string path = temp.File("secrets.json");
        PrivateFile.Write(path, content, NullLogger.Instance);
        File.SetUnixFileMode(path, groupReadable);

        using (PrivateFile.OpenRead(path, NullLogger.Instance)) {
        }

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldWarnWithFileNameOnly_WhenTighteningMode() {
        string path = temp.File("secrets.json");
        PrivateFile.Write(path, content, NullLogger.Instance);
        File.SetUnixFileMode(path, groupReadable);
        FakeLogger logger = new();

        using (PrivateFile.OpenRead(path, logger)) {
        }

        Assert.Equal(
            ["paths.private_file.tightened tightened file mode to 0600 file=secrets.json"],
            logger.Collector.GetSnapshot().Select(record => $"{record.Id.Name} {record.Message}"));
    }

    private static byte[] ReadAll(FileStream stream) {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static void MakeFifo(string path) {
        using Process process = Process.Start("mkfifo", [path]);
        process.WaitForExit();
    }
}
