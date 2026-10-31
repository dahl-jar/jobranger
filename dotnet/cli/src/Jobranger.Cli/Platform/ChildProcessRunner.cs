using System.ComponentModel;
using System.Diagnostics;

namespace Jobranger.Cli.Platform;

internal sealed class ChildProcessRunner : IChildProcessRunner {
    private const int bufferSize = 81_920;
    private const int noExitCode = -1;

    private static readonly TimeSpan settleGrace = TimeSpan.FromSeconds(2);

    public async Task<ChildExit> RunAsync(ChildRequest request, CancellationToken cancellationToken) {
        using Process process = Process.Start(StartInfo(request))
            ?? throw new InvalidOperationException($"child process did not start program={request.Program}");
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Deadline);
        Task errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
        Task input = WriteInputAsync(process.StandardInput.BaseStream, request.Input);
        Task<(byte[] Output, bool TooLarge)> reading = ReadOutputAsync(process.StandardOutput.BaseStream, request.OutputLimit);
        byte[] output = [];
        bool tooLarge = false;
        bool timedOut = false;
        try {
            (output, tooLarge) = await reading.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (tooLarge) {
                KillTree(process);
            }
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
            timedOut = true;
        } finally {
            KillTree(process);
            await SettleAsync(Task.WhenAll(errors, input, reading)).ConfigureAwait(false);
        }
        int exitCode = process.WaitForExit(settleGrace) ? process.ExitCode : noExitCode;
        return new ChildExit(exitCode, timedOut ? [] : output, timedOut, tooLarge);
    }

    public static void KillTree(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception error) when (error is InvalidOperationException or Win32Exception) {
            return;
        }
    }

    private static ProcessStartInfo StartInfo(ChildRequest request) {
        ProcessStartInfo start = new(request.Program) {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory,
        };
        foreach (string argument in request.Arguments) {
            start.ArgumentList.Add(argument);
        }
        start.Environment.Clear();
        foreach (KeyValuePair<string, string> variable in request.Environment) {
            start.Environment[variable.Key] = variable.Value;
        }
        return start;
    }

    private static async Task WriteInputAsync(Stream stdin, ReadOnlyMemory<byte> input) {
        await using (stdin.ConfigureAwait(false)) {
            try {
                await stdin.WriteAsync(input, CancellationToken.None).ConfigureAwait(false);
            } catch (IOException) {
                return;
            }
        }
    }

    private static async Task<(byte[] Output, bool TooLarge)> ReadOutputAsync(Stream stdout, int limit) {
        using MemoryStream output = new();
        byte[] buffer = new byte[bufferSize];
        int read;
        while ((read = await stdout.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0) {
            int room = limit - (int)output.Length;
            if (read > room) {
                output.Write(buffer, 0, room);
                return (output.ToArray(), true);
            }
            output.Write(buffer, 0, read);
        }
        return (output.ToArray(), false);
    }

    private static async Task SettleAsync(Task pending) {
        try {
            await pending.WaitAsync(settleGrace).ConfigureAwait(false);
        } catch (Exception error) when (error is IOException or TimeoutException or ObjectDisposedException) {
            return;
        }
    }
}
