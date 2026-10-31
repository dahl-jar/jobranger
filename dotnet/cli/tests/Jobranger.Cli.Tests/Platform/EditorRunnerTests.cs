using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Platform;

[UnsupportedOSPlatform("windows")]
public sealed class EditorRunnerTests : IDisposable {
    private const string suffix = ".md";
    private const string input = "old";
    private const string visual = "VISUAL";
    private const string editor = "EDITOR";
    private const int failedExit = 3;
    private const string cleanupFailedLine = @"^edit folder not removed dir=edit-[^/ ]+ error_type=\w+Exception$";

    private readonly TempDir temp = new();
    private readonly string stateDir;

    public EditorRunnerTests() {
        stateDir = temp.File("state");
        Directory.CreateDirectory(stateDir);
    }

    public void Dispose() => temp.Dispose();

    [Theory]
    [InlineData("nano", "ed", "nano")]
    [InlineData(null, "ed", "ed")]
    [InlineData("  ", "ed", "ed")]
    [InlineData(null, null, "vi")]
    public async Task ShouldPickEditorInOrder_WhenVariablesAreSet(string? visualValue, string? editorValue, string expected) {
        FakeEditorProcess process = EditFixtures.Exits(0);
        Hashtable environment = new() { [visual] = visualValue, [editor] = editorValue };

        await Runner(process, environment).EditAsync(input, suffix, TestContext.Current.CancellationToken);

        Assert.Equal(expected, process.Starts[0].Program);
    }

    [Fact]
    public async Task ShouldPassFileLast_WhenEditorHasArguments() {
        FakeEditorProcess process = EditFixtures.Exits(0);

        await Runner(process, new Hashtable { [visual] = "code --wait" })
            .EditAsync(input, suffix, TestContext.Current.CancellationToken);

        (string program, IReadOnlyList<string> arguments) = process.Starts[0];
        Assert.Equal("code", program);
        Assert.Equal("--wait", arguments[0]);
        Assert.EndsWith(suffix, arguments[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnEditedText_WhenEditorExitsZero() {
        FakeEditorProcess process = new(file => {
            File.WriteAllText(file, "new\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return 0;
        });

        string edited = await Runner(process).EditAsync(input, suffix, TestContext.Current.CancellationToken);

        Assert.Equal("new\n", edited);
    }

    [Fact]
    public async Task ShouldOpenPrivateFile_WhenEditorStarts() {
        (UnixFileMode File, UnixFileMode Folder, string Text, string Name) seen = default;
        FakeEditorProcess process = new(file => {
            seen = (
                File.GetUnixFileMode(file),
                File.GetUnixFileMode(Path.GetDirectoryName(file)!),
                File.ReadAllText(file),
                Path.GetFileName(file));
            return 0;
        });

        await Runner(process).EditAsync(input, suffix, TestContext.Current.CancellationToken);

        Assert.Equal((FileModes.OwnerReadWrite, FileModes.OwnerOnlyDir, input, "edit" + suffix), seen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(failedExit)]
    public async Task ShouldRemoveEditFolder_WhenEditorExits(int exit) {
        await Record.ExceptionAsync(
            () => Runner(EditFixtures.Exits(exit)).EditAsync(input, suffix, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFileSystemEntries(stateDir));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldThrowFailedExit_WhenFailedEditorLeavesFileUnreadable(bool deleted) {
        FakeEditorProcess process = new(file => {
            if (deleted) {
                File.Delete(file);
            } else {
                File.WriteAllBytes(file, Utf8Fixtures.WithInvalidByte("a", "b"));
            }
            return failedExit;
        });

        EditorException error = await Assert.ThrowsAsync<EditorException>(
            () => Runner(process).EditAsync(input, suffix, TestContext.Current.CancellationToken));

        Assert.Equal((EditorProblem.FailedExit, null), (error.Problem, error.EditedText));
    }

    [Fact]
    public async Task ShouldThrowFailedExit_WhenEditIsCancelled() {
        using CancellationTokenSource cancelled = new();
        FakeEditorProcess process = new(_ => {
            cancelled.Cancel();
            cancelled.Token.ThrowIfCancellationRequested();
            return 0;
        });

        EditorException error = await Assert.ThrowsAsync<EditorException>(
            () => Runner(process).EditAsync(input, suffix, cancelled.Token));

        Assert.Equal(EditorProblem.FailedExit, error.Problem);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldThrowTempFile_WhenEditedFileIsUnreadable(bool deleted) {
        FakeEditorProcess process = new(file => {
            if (deleted) {
                File.Delete(file);
            } else {
                File.WriteAllBytes(file, Utf8Fixtures.WithInvalidByte("a", "b"));
            }
            return 0;
        });

        EditorException error = await Assert.ThrowsAsync<EditorException>(
            () => Runner(process).EditAsync(input, suffix, TestContext.Current.CancellationToken));

        Assert.Equal(EditorProblem.TempFile, error.Problem);
    }

    [Fact]
    public async Task ShouldThrowTempFile_WhenEditFileCannotBeWritten() {
        File.WriteAllText(temp.File("blocked"), string.Empty);
        FakeEditorProcess process = EditFixtures.Exits(0);
        EditorRunner runner = new(new Hashtable(), temp.File("blocked"), process, NullLogger.Instance);

        EditorException error = await Assert.ThrowsAsync<EditorException>(
            () => runner.EditAsync(input, suffix, TestContext.Current.CancellationToken));

        Assert.Equal(EditorProblem.TempFile, error.Problem);
        Assert.Empty(process.Starts);
    }

    [Fact]
    public async Task ShouldKeepEditedText_WhenEditFolderCannotBeRemoved() {
        FakeEditorProcess process = new(file => {
            File.WriteAllText(file, "new");
            File.SetUnixFileMode(stateDir, EditFixtures.ReadOnlyDir);
            return 0;
        });

        FakeLogger logger = new();
        EditorRunner runner = new(new Hashtable(), stateDir, process, logger);

        string edited = await runner.EditAsync(input, suffix, TestContext.Current.CancellationToken);

        File.SetUnixFileMode(stateDir, FileModes.OwnerOnlyDir);
        Assert.Equal(("new", "edit.cleanup_failed"), (edited, logger.LatestRecord.Id.Name));
        Assert.Matches(cleanupFailedLine, logger.LatestRecord.Message);
    }

    private EditorRunner Runner(IEditorProcess process, IDictionary? environment = null) =>
        new(environment ?? new Hashtable(), stateDir, process, NullLogger.Instance);
}
