using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Platform;

[UnsupportedOSPlatform("windows")]
[Collection(currentFolderCollection)]
public sealed class EditorProcessTests : IDisposable {
    private const string currentFolderCollection = "current folder";
    private const string shell = "/bin/sh";
    private const string runScript = "-c";
    private const string failedExitScript = "exit 3";
    private const int failedExit = 3;
    private const string pathVariable = "PATH";
    private const string editorName = "vi";
    private const string relativeEditor = "./vi";
    private const string pathText = "from PATH";
    private const string currentText = "from current folder";
    private const string linkLoopEntry = "link loop";
    private const string pipeEntry = "pipe";
    private const string lockedFolderEntry = "locked folder";
    private const string othersOnlyEntry = "others only";
    private const UnixFileMode runnable = FileModes.OwnerOnlyDir;
    private const UnixFileMode othersOnly =
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly TempDir temp = new();
    private readonly EditorProcess process = new(new Hashtable());
    private readonly string startFolder = Directory.GetCurrentDirectory();

    public void Dispose() {
        Directory.SetCurrentDirectory(startFolder);
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldReturnExitCode_WhenProgramExits() {
        int exit = await process.RunAsync(shell, [runScript, failedExitScript], TestContext.Current.CancellationToken);

        Assert.Equal(failedExit, exit);
    }

    [Fact]
    public async Task ShouldLetEditorFinish_WhenCancelled() {
        string processFile = temp.File("pid");
        string marker = temp.File("marker");
        string script = $"trap 'sleep 0.2; echo done > {marker}; exit 0' TERM; echo $$ > {processFile}; "
            + "while :; do sleep 0.1; done";
        using CancellationTokenSource cancelled = new();
        Task<int> running = process.RunAsync(shell, [runScript, script], cancelled.Token);
        await WaitForProcessIdAsync(processFile);

        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task ShouldKillEditor_WhenEditorIgnoresTerminate() {
        string processFile = temp.File("pid");
        using CancellationTokenSource cancelled = new();
        Task<int> running = process.RunAsync(
            shell, [runScript, $"trap '' TERM; echo $$ > {processFile}; sleep 30"], cancelled.Token);
        int processId = await WaitForProcessIdAsync(processFile);

        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await TestWaits.UntilAsync(() => IsGone(processId));
    }

    [Fact]
    public async Task ShouldRestoreTerminal_WhenCancelled() {
        using Pseudoterminal pseudoterminal = new();
        string processFile = temp.File("pid");
        EditorProcess editor = new(new Hashtable(), pseudoterminal.Descriptor);
        string script = $"stty -echo < {pseudoterminal.Path}; echo $$ > {processFile}; exec sleep 30";
        using CancellationTokenSource cancelled = new();
        Task<int> running = editor.RunAsync(shell, [runScript, script], cancelled.Token);
        await WaitForProcessIdAsync(processFile);

        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(pseudoterminal.IsEchoOn());
    }

    [Theory]
    [InlineData(linkLoopEntry)]
    [InlineData(pipeEntry)]
    [InlineData(lockedFolderEntry)]
    [InlineData(othersOnlyEntry)]
    public void ShouldPickNextFolder_WhenEntryIsNotRunnable(string entry) {
        string first = Folder("first");
        string second = Folder("second");
        string expected = WriteScript(second, pathText);
        AddEntry(first, entry);

        string found = EditorProcess.Resolve(editorName, $"{first}{Path.PathSeparator}{second}");

        File.SetUnixFileMode(first, FileModes.OwnerOnlyDir);
        Assert.Equal(expected, found);
    }

    [Fact]
    public void ShouldPickLink_WhenLinkTargetIsRunnable() {
        string target = WriteScript(Folder("target"), pathText);
        string link = Path.Combine(Folder("links"), editorName);
        File.CreateSymbolicLink(link, target);

        string found = EditorProcess.Resolve(editorName, Path.GetDirectoryName(link));

        Assert.Equal(link, found);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".")]
    public void ShouldThrowNotFound_WhenOnlyCurrentFolderHasProgram(string? searchPath) {
        string current = Folder("current");
        WriteScript(current, currentText);
        Directory.SetCurrentDirectory(current);

        Assert.Throws<Win32Exception>(() => EditorProcess.Resolve(editorName, searchPath));
    }

    [Fact]
    public void ShouldKeepProgram_WhenValueHasFolder() {
        string found = EditorProcess.Resolve(relativeEditor, null);

        Assert.Equal(relativeEditor, found);
    }

    [Fact]
    public async Task ShouldOpenPathEditor_WhenCurrentFolderHasSameName() {
        string bin = Folder("bin");
        string current = Folder("current");
        WriteScript(bin, pathText);
        WriteScript(current, currentText);
        Directory.SetCurrentDirectory(current);
        Hashtable environment = new() { [pathVariable] = bin };
        EditorRunner runner = new(environment, temp.File("state"), new EditorProcess(environment), NullLogger.Instance);

        string edited = await runner.EditAsync("old", ".md", TestContext.Current.CancellationToken);

        Assert.Equal(pathText, edited);
    }

    private static async Task<int> WaitForProcessIdAsync(string processFile) {
        await TestWaits.UntilAsync(() => File.Exists(processFile) && File.ReadAllText(processFile).EndsWith('\n'));
        return int.Parse(File.ReadAllText(processFile), CultureInfo.InvariantCulture);
    }

    private static string WriteScript(string folder, string text) {
        string path = Path.Combine(folder, editorName);
        File.WriteAllText(path, $"#!/bin/sh\nprintf '{text}' > \"$1\"\n");
        File.SetUnixFileMode(path, runnable);
        return path;
    }

    private static void AddEntry(string folder, string entry) {
        string path = Path.Combine(folder, editorName);
        switch (entry) {
            case othersOnlyEntry:
                File.WriteAllText(path, string.Empty);
                File.SetUnixFileMode(path, othersOnly);
                return;
            case linkLoopEntry:
                File.CreateSymbolicLink(path, path);
                return;
            case pipeEntry:
                using (Process pipe = Process.Start("mkfifo", [path])) {
                    pipe.WaitForExit();
                }
                File.SetUnixFileMode(path, runnable);
                return;
            default:
                WriteScript(folder, currentText);
                File.SetUnixFileMode(folder, UnixFileMode.None);
                return;
        }
    }

    private static bool IsGone(int processId) {
        try {
            using Process found = Process.GetProcessById(processId);
            return found.HasExited;
        } catch (ArgumentException) {
            return true;
        }
    }

    private string Folder(string name) {
        string path = temp.File(name);
        Directory.CreateDirectory(path);
        return path;
    }

    [CollectionDefinition(currentFolderCollection, DisableParallelization = true)]
    public sealed class CurrentFolderRuns;
}
