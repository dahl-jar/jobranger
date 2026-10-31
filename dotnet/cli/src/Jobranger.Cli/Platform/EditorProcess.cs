using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;

namespace Jobranger.Cli.Platform;

[UnsupportedOSPlatform("windows")]
internal sealed class EditorProcess(IDictionary environment, int terminal) : IEditorProcess {
    private const string pathVariable = "PATH";
    private const int standardInput = 0;

    private static readonly PosixSignal[] heldSignals =
        [PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM, PosixSignal.SIGHUP];
    private static readonly TimeSpan stopGrace = TimeSpan.FromSeconds(1);

    public EditorProcess(IDictionary environment) : this(environment, standardInput) {
    }

    public static string Resolve(string program, string? searchPath) {
        if (program.Contains(Path.DirectorySeparatorChar)) {
            return program;
        }
        return (searchPath ?? string.Empty).Split(Path.PathSeparator)
            .Where(Path.IsPathRooted)
            .Select(folder => Path.Combine(folder, program))
            .FirstOrDefault(IsRunnable)
            ?? throw new Win32Exception($"editor not found on PATH program={program}");
    }

    public async Task<int> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) {
        string resolved = Resolve(program, EnvironmentVariables.Read(environment, pathVariable));
        byte[]? settings = UnixFiles.ReadTerminalSettings(terminal);
        List<PosixSignalRegistration> held = [
            .. heldSignals.Select(signal => PosixSignalRegistration.Create(signal, context => context.Cancel = true)),
        ];
        try {
            using Process process = Process.Start(StartInfo(resolved, arguments))
                ?? throw new InvalidOperationException($"editor process did not start program={program}");
            try {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                Stop(process, settings);
                throw;
            }
            return process.ExitCode;
        } finally {
            held.ForEach(registration => registration.Dispose());
        }
    }

    private void Stop(Process process, byte[]? settings) {
        UnixFiles.Terminate(process.Id);
        process.WaitForExit(stopGrace);
        ChildProcessRunner.KillTree(process);
        if (settings is not null && !UnixFiles.IsBackground(terminal)) {
            UnixFiles.WriteTerminalSettings(terminal, settings);
        }
    }

    private static bool IsRunnable(string path) {
        FileInfo file = new(path);
        try {
            FileSystemInfo target = file.ResolveLinkTarget(returnFinalTarget: true) ?? file;
            return UnixFiles.IsRegularPath(target.FullName) && UnixFiles.CanExecute(target.FullName);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return false;
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
