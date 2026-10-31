using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal sealed class TerminalEdit(string folder) : IDisposable {
    private const string shell = "/bin/sh";
    private const string sessionProgram = "script";
    private const string discardedTranscript = "/dev/null";
    private const string configVariable = "JOBRANGER_CONFIG_DIR";
    private const string dataVariable = "JOBRANGER_DATA_DIR";
    private const string stateVariable = "JOBRANGER_STATE_DIR";
    private const string visualVariable = "VISUAL";
    private const string editFolderPattern = "edit-*";
    private const string settleSeconds = "0.5";
    private const char lineEnd = '\n';
    private const UnixFileMode runnable = FileModes.OwnerOnlyDir;

    private readonly string ready = Path.Combine(folder, "ready");
    private readonly string exitFile = Path.Combine(folder, "exit");
    private readonly string settingsFile = Path.Combine(folder, "settings");
    private readonly string stateFolder = Path.Combine(folder, "state");
    private Process? session;

    public Task<int> RunInBackgroundAsync() =>
        RunAsync(
            $": > {ready}\nexec sleep 30\n",
            "set -m\n"
            + $"{EditCommand()} > /dev/null 2>&1 &\n"
            + "job=$!\n"
            + $"until [ -e {ready} ]; do sleep 0.05; done\n"
            + $"sleep {settleSeconds}\n"
            + "kill -TERM $job\n"
            + "wait $job\n"
            + $"echo $? > {exitFile}\n");

    public Task<int> RunInForegroundAsync() =>
        RunAsync(
            $"stty -echo\n: > {ready}\nsleep {settleSeconds}\nkill -TERM $PPID\nexec sleep 30\n",
            $"{EditCommand()} > /dev/null 2>&1\n"
            + "code=$?\n"
            + $"stty -a > {settingsFile}\n"
            + $"echo $code > {exitFile}\n");

    public string[] EditFolders() => Directory.GetDirectories(stateFolder, editFolderPattern);

    public bool IsEchoOn() => Pseudoterminal.HasEcho(File.ReadAllText(settingsFile));

    public void Dispose() {
        if (session is { HasExited: false }) {
            session.Kill(entireProcessTree: true);
        }
        session?.Dispose();
    }

    private async Task<int> RunAsync(string editorScript, string jobScript) {
        string jobFile = Path.Combine(folder, "job.sh");
        File.WriteAllText(jobFile, jobScript);
        session = Process.Start(SessionStart(jobFile, editorScript))
            ?? throw new InvalidOperationException("terminal session did not start");
        session.BeginOutputReadLine();
        await TestWaits.UntilAsync(() => File.Exists(ready));
        await TestWaits.UntilAsync(() => File.Exists(exitFile) && File.ReadAllText(exitFile).EndsWith(lineEnd));
        return int.Parse(File.ReadAllText(exitFile), CultureInfo.InvariantCulture);
    }

    private ProcessStartInfo SessionStart(string jobFile, string editorScript) {
        ProcessStartInfo start = new(sessionProgram, SessionArguments(jobFile)) {
            WorkingDirectory = folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        start.Environment[configVariable] = Path.Combine(folder, "config");
        start.Environment[dataVariable] = Path.Combine(folder, "data");
        start.Environment[stateVariable] = stateFolder;
        start.Environment[visualVariable] = WriteEditor(editorScript);
        return start;
    }

    private static string[] SessionArguments(string jobFile) =>
        OperatingSystem.IsMacOS()
            ? ["-q", discardedTranscript, shell, jobFile]
            : ["-qfc", $"{shell} {jobFile}", discardedTranscript];

    private static string EditCommand() =>
        $"{CvFixtures.Muxer} {CvFixtures.WorkerDll} guidance edit";

    private string WriteEditor(string script) {
        string editor = Path.Combine(folder, "editor");
        File.WriteAllText(editor, $"#!/bin/sh\n{script}");
        File.SetUnixFileMode(editor, runnable);
        return editor;
    }
}
