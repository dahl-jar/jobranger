using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class CliProcess : IAsyncDisposable {
    private const string shell = "/bin/sh";
    private const string shellCommand = "-c";
    private const string killScript = "kill \"$0\" \"$1\"";

    private readonly Process process;
    private readonly ConcurrentQueue<string> output = new();

    private CliProcess(Process process) {
        this.process = process;
        process.OutputDataReceived += (_, line) => Keep(line.Data);
        process.ErrorDataReceived += (_, line) => Keep(line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public int ExitCode => process.ExitCode;

    public string Output => string.Join(Environment.NewLine, output);

    public static CliProcess Start(Hashtable environment, params string[] arguments) {
        ProcessStartInfo start = new(CvFixtures.Muxer, [CvFixtures.WorkerDll, .. arguments]) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Clear();
        foreach (DictionaryEntry entry in environment) {
            start.Environment[(string)entry.Key] = (string?)entry.Value;
        }
        return new CliProcess(Process.Start(start) ?? throw new InvalidOperationException("the CLI did not start"));
    }

    public async Task<TimeSpan> SignalAsync(string flag) {
        CancellationToken test = TestContext.Current.CancellationToken;
        Stopwatch elapsed = Stopwatch.StartNew();
        string target = process.Id.ToString(CultureInfo.InvariantCulture);
        ProcessStartInfo kill = new(shell, [shellCommand, killScript, flag, target]);
        using (Process sender = Process.Start(kill) ?? throw new InvalidOperationException("kill did not start")) {
            await sender.WaitForExitAsync(test).WaitAsync(TestWaits.Step, test);
        }
        await process.WaitForExitAsync(test).WaitAsync(TestWaits.Step, test);
        elapsed.Stop();
        return elapsed.Elapsed;
    }

    public async ValueTask DisposeAsync() {
        if (!process.HasExited) {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TestWaits.Step, CancellationToken.None);
        process.Dispose();
    }

    private void Keep(string? line) {
        if (line is not null) {
            output.Enqueue(line);
        }
    }
}
