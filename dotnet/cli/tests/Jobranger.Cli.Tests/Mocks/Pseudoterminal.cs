using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal sealed partial class Pseudoterminal : IDisposable {
    private const string libc = "libc";
    private const string linuxLibc = "libc.so.6";
    private const int readWrite = 0x2;
    private const int macNoControllingTerminal = 0x20000;
    private const int linuxNoControllingTerminal = 0x100;
    private const string shell = "/bin/sh";
    private const string runScript = "-c";
    private const string echoFlag = "echo";

    private readonly SafeFileHandle controller;
    private readonly SafeFileHandle terminal;

    static Pseudoterminal() {
        NativeLibrary.SetDllImportResolver(
            typeof(Pseudoterminal).Assembly,
            (_, _, _) => NativeLibrary.Load(OperatingSystem.IsMacOS() ? libc : linuxLibc));
    }

    public Pseudoterminal() {
        int noControllingTerminal = OperatingSystem.IsMacOS() ? macNoControllingTerminal : linuxNoControllingTerminal;
        controller = new SafeFileHandle(OpenController(readWrite | noControllingTerminal), ownsHandle: true);
        if (Grant(controller) != 0 || Unlock(controller) != 0) {
            throw new InvalidOperationException("pseudoterminal not unlocked");
        }
        Path = Marshal.PtrToStringUTF8(TerminalName(controller))
            ?? throw new InvalidOperationException("pseudoterminal has no name");
        terminal = File.OpenHandle(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    public string Path { get; }

    public int Descriptor => (int)terminal.DangerousGetHandle();

    public bool IsEchoOn() {
        ProcessStartInfo start = new(shell, [runScript, $"stty -a < {Path}"]) { RedirectStandardOutput = true };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("stty did not start");
        string[] flags = process.StandardOutput.ReadToEnd().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        process.WaitForExit();
        return flags.Contains(echoFlag);
    }

    public void Dispose() {
        terminal.Dispose();
        controller.Dispose();
    }

    [LibraryImport(libc, EntryPoint = "posix_openpt")]
    private static partial int OpenController(int flags);

    [LibraryImport(libc, EntryPoint = "grantpt")]
    private static partial int Grant(SafeFileHandle controller);

    [LibraryImport(libc, EntryPoint = "unlockpt")]
    private static partial int Unlock(SafeFileHandle controller);

    [LibraryImport(libc, EntryPoint = "ptsname")]
    private static partial nint TerminalName(SafeFileHandle controller);
}
