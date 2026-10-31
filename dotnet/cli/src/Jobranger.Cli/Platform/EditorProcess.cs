using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jobranger.Cli.Platform;

internal sealed class EditorProcess : IEditorProcess {
    private static readonly PosixSignal[] heldSignals =
        [PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM, PosixSignal.SIGHUP];

    public async Task<int> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) {
        List<PosixSignalRegistration> held = [
            .. heldSignals.Select(signal => PosixSignalRegistration.Create(signal, context => context.Cancel = true)),
        ];
        try {
            using Process process = Process.Start(StartInfo(program, arguments))
                ?? throw new InvalidOperationException($"editor process did not start program={program}");
            try {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                ChildProcessRunner.KillTree(process);
                throw;
            }
            return process.ExitCode;
        } finally {
            held.ForEach(registration => registration.Dispose());
        }
    }

    private static ProcessStartInfo StartInfo(string program, IReadOnlyList<string> arguments) {
        ProcessStartInfo start = new(program) { UseShellExecute = false };
        foreach (string argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        return start;
    }
}
