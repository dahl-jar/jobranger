using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jobranger.Relay.Tests;

public sealed class StartupProcessTests {
    private const int startupFailureExitCode = 1;

    private static readonly TimeSpan exitDeadline = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ShouldExitWithFailureCodeOnInvalidConfig() {
        string workDir = Directory.CreateTempSubdirectory("relay-startup-").FullName;
        ProcessStartInfo start = new(DotnetHost(), [Path.Combine(AppContext.BaseDirectory, "Jobranger.Relay.dll")]) {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["Relay__KnownProxyNetworks__0"] = "10.0.0.0/8";
        using Process relay = Process.Start(start)!;
        Task<string> output = relay.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> errors = relay.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource deadline = new(exitDeadline);

        bool exited = await WaitForExit(relay, deadline.Token);

        Assert.Equal((true, startupFailureExitCode), (exited, exited ? relay.ExitCode : -1));
        Assert.Contains("relay failed to start", await output + await errors, StringComparison.Ordinal);
    }

    private static async Task<bool> WaitForExit(Process relay, CancellationToken deadline) {
        try {
            await relay.WaitForExitAsync(deadline);
            return true;
        } catch (OperationCanceledException) {
            relay.Kill(entireProcessTree: true);
            return false;
        }
    }

    private static string DotnetHost() => Path.GetFullPath(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(),
        "..",
        "..",
        "..",
        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
