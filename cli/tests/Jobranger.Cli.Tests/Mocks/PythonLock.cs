using System.Diagnostics;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class PythonLock : IDisposable {
    public const string Held = "held";
    public const string Blocked = "blocked";

    private const string python = "python3";
    private const string script = """
        import fcntl, os, sys
        descriptor = os.open(sys.argv[1], os.O_RDWR | os.O_CREAT, 0o600)
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print("blocked", flush=True)
            sys.exit(0)
        print("held", flush=True)
        sys.stdin.read()
        """;

    private readonly Process process;

    private PythonLock(string path) {
        process = Process.Start(new ProcessStartInfo(python, ["-c", script, path]) {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        })!;
        State = process.StandardOutput.ReadLine();
    }

    public string? State { get; }

    public static PythonLock TryAcquire(string path) => new(path);

    public void Dispose() {
        process.StandardInput.Close();
        process.WaitForExit();
        process.Dispose();
    }
}
