using System.Collections;
using System.Diagnostics;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class PtyProcess : IAsyncDisposable {
    private const string scriptProgram = "/usr/bin/script";
    private const string shellProgram = "/bin/sh";
    private const string nullDevice = "/dev/null";
    private const string terminalVariable = "TERM";
    private const string terminalType = "xterm-256color";
    private const int bufferChars = 4096;

    private static readonly TimeSpan keyboardReady = TimeSpan.FromMilliseconds(300);

    private readonly Process process;
    private readonly StringBuilder transcript = new();
    private readonly Task reading;

    private PtyProcess(Process process) {
        this.process = process;
        reading = ReadAsync();
    }

    public int ExitCode => process.ExitCode;

    public string Transcript {
        get {
            lock (transcript) {
                return transcript.ToString();
            }
        }
    }

    public static PtyProcess Start(Hashtable environment, params string[] arguments) =>
        StartCommand(environment, Command(arguments));

    public static PtyProcess StartWithOutputFile(Hashtable environment, string outputFile, params string[] arguments) =>
        StartCommand(environment, $"{Command(arguments)} > {Quoted(outputFile)}");

    public static PtyProcess StartWithErrorFile(Hashtable environment, string errorFile, params string[] arguments) =>
        StartCommand(environment, $"{Command(arguments)} 2> {Quoted(errorFile)}");

    public async Task WaitForAsync(string text) {
        try {
            await TestWaits.UntilAsync(() => Transcript.Contains(text, StringComparison.Ordinal));
        } catch (OperationCanceledException) {
            Assert.Fail($"no \"{text}\" arrived. Transcript: {Transcript}");
        }
    }

    public async Task WaitForExitAsync() {
        await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
        await reading.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
    }

    public async Task SendAsync(string keys) {
        await Task.Delay(keyboardReady, TestContext.Current.CancellationToken);
        await process.StandardInput.WriteAsync(keys);
        await process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
        using (process) {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TestWaits.Step, CancellationToken.None);
        }
    }

    private static string Command(string[] arguments) =>
        "exec " + string.Join(' ', new[] { CvFixtures.Muxer, CvFixtures.WorkerDll }.Concat(arguments).Select(Quoted));

    private static string Quoted(string value) => $"'{value}'";

    private static PtyProcess StartCommand(Hashtable environment, string command) {
        string[] arguments = OperatingSystem.IsLinux()
            ? ["-qfec", command, nullDevice]
            : ["-q", nullDevice, shellProgram, "-c", command];
        ProcessStartInfo start = new(scriptProgram, arguments) {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        start.Environment.Clear();
        start.Environment[terminalVariable] = terminalType;
        foreach (DictionaryEntry entry in environment) {
            start.Environment[(string)entry.Key] = (string?)entry.Value;
        }
        return new PtyProcess(Process.Start(start) ?? throw new InvalidOperationException("script did not start"));
    }

    private async Task ReadAsync() {
        char[] buffer = new char[bufferChars];
        while (true) {
            int read = await process.StandardOutput.ReadAsync(buffer, CancellationToken.None);
            if (read == 0) {
                return;
            }
            lock (transcript) {
                transcript.Append(buffer, 0, read);
            }
        }
    }
}
