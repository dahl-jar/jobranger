using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Secrets;

[UnsupportedOSPlatform("windows")]
public sealed class FileSecretStoreTests : IDisposable {
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;
    private const UnixFileMode unlistableDir = UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const int parallelCallers = 8;
    private const int lockRounds = 20;

    private static readonly Dictionary<string, string> secrets =
        new() { [SecretFixtures.SecretName] = SecretFixtures.FileValue };

    private readonly TempDir temp = new();
    private readonly FileSecretStore file;

    public FileSecretStoreTests() {
        file = new FileSecretStore(temp.File(SecretFixtures.FileName), NullLogger.Instance);
    }

    public void Dispose() {
        File.SetUnixFileMode(temp.Path, FileModes.OwnerOnlyDir);
        temp.Dispose();
    }

    [Theory]
    [InlineData($"{{\"{SecretFixtures.SecretName}\": \"{SecretFixtures.FileValue}\"", "not UTF-8 JSON")]
    [InlineData("[]", "not a JSON object of strings")]
    [InlineData($"{{\"{SecretFixtures.SecretName}\": 1234}}", "not a JSON object of strings")]
    [InlineData($"{{\"{SecretFixtures.SecretName}\": \"sk-fake-\u00ff-1234\"}}", "not UTF-8 JSON")]
    [InlineData($"{{\"{SecretFixtures.SecretName}\": \"\\ud800\"}}", "not UTF-8 JSON")]
    public void ShouldNameReason_WhenSecretsFileIsDamaged(string content, string reason) {
        File.WriteAllText(file.Path, content, Encoding.Latin1);

        PrivateFileException refused = Assert.Throws<PrivateFileException>(() => file.Load());

        Assert.Equal(reason, refused.Reason);
    }

    [Fact]
    public void ShouldRefuseFile_WhenPathIsSymlink() {
        using TempDir outside = new();
        File.WriteAllText(outside.File("other.json"), "{}");
        File.CreateSymbolicLink(file.Path, outside.File("other.json"));

        PrivateFileException refused = Assert.Throws<PrivateFileException>(() => file.Load());

        Assert.Equal("symbolic link", refused.Reason);
    }

    [Fact]
    public void ShouldWriteFileWithMode0600_WhenSaving() {
        file.Save(secrets);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(file.Path));
    }

    [Fact]
    public void ShouldReleaseLock_WhenStaleTempCleanupFails() {
        File.SetUnixFileMode(temp.Path, unlistableDir);
        Assert.Throws<PrivateFileException>(() => file.Lock(TestContext.Current.CancellationToken));
        File.SetUnixFileMode(temp.Path, FileModes.OwnerOnlyDir);

        Exception? error = Record.Exception(
            () => FileLock.Acquire(file.Path + ".lock", TestWaits.Step, NullLogger.Instance, TestContext.Current.CancellationToken)
                .Dispose());

        Assert.Null(error);
    }

    [Fact]
    public async Task ShouldWaitForLock_WhenCallersCreateLockInParallel() {
        Exception? error = await Record.ExceptionAsync(LockFreshFilesInParallel);

        Assert.Null(error);
    }

    [Fact]
    public void ShouldNamePath_WhenFileCannotBeWritten() {
        using (file.Lock(TestContext.Current.CancellationToken)) {
            File.SetUnixFileMode(temp.Path, readOnlyDir);

            PrivateFileException refused = Assert.Throws<PrivateFileException>(() => file.Save(secrets));

            Assert.Equal(file.Path, refused.Path);
        }
    }

    private async Task LockFreshFilesInParallel() {
        for (int round = 0; round < lockRounds; round++) {
            FileSecretStore fresh = new(temp.File($"{round}-{SecretFixtures.FileName}"), NullLogger.Instance);
            await Task.WhenAll(Enumerable.Range(0, parallelCallers).Select(_ => Task.Run(() => {
                using FileLock held = fresh.Lock(TestContext.Current.CancellationToken);
            })));
        }
    }
}
