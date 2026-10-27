using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class RelayProcess : IAsyncDisposable {
    public const string SiteOrigin = "https://site.example.test";

    private const string relayAssemblyKey = "RelayAssembly";
    private const string healthRoute = "healthz";
    private const string proxyNetwork = "10.42.0.0/16";
    private const string urlsKey = "ASPNETCORE_URLS";
    private const string siteOriginKey = "Relay__SiteOrigin";
    private const string handleKeyKey = "Relay__HandleKey";
    private const string redactionKeyKey = "Relay__RedactionKey";
    private const string proxyNetworkKey = "Relay__KnownProxyNetworks__0";
    private const char slash = '/';
    private const int keyBytes = 32;

    private static readonly TimeSpan startDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(50);

    private readonly string handleKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(keyBytes));
    private readonly string redactionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(keyBytes));
    private readonly ConcurrentQueue<string> output = new();
    private readonly int port = FreePort();
    private Process? process;

    public Uri Address => new($"http://127.0.0.1:{port}/");

    public async Task StartAsync() {
        string assembly = RelayAssembly();
        ProcessStartInfo start = new(DotnetHost(), [assembly]) {
            WorkingDirectory = Path.GetDirectoryName(assembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment[urlsKey] = Address.AbsoluteUri.TrimEnd(slash);
        start.Environment[siteOriginKey] = SiteOrigin;
        start.Environment[handleKeyKey] = handleKey;
        start.Environment[redactionKeyKey] = redactionKey;
        start.Environment[proxyNetworkKey] = proxyNetwork;
        process = Process.Start(start) ?? throw new InvalidOperationException("relay process did not start");
        process.OutputDataReceived += (_, line) => Keep(line.Data);
        process.ErrorDataReceived += (_, line) => Keep(line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try {
            await WaitHealthyAsync(process);
        } catch {
            await StopAsync();
            throw;
        }
    }

    public async Task StopAsync() {
        if (process is null) {
            return;
        }
        if (!process.HasExited) {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync(CancellationToken.None);
        process.Dispose();
        process = null;
    }

    public async Task RestartAsync() {
        await StopAsync();
        await StartAsync();
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task WaitHealthyAsync(Process started) {
        using HttpClient http = new() { BaseAddress = Address };
        using CancellationTokenSource deadline =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(startDeadline);
        while (true) {
            if (started.HasExited) {
                throw new InvalidOperationException($"relay exited code={started.ExitCode} output={string.Join('\n', output)}");
            }
            try {
                using HttpResponseMessage health = await http.GetAsync(healthRoute, deadline.Token);
                if (health.StatusCode == HttpStatusCode.OK) {
                    return;
                }
            } catch (HttpRequestException) {
            }
            await Task.Delay(pollInterval, deadline.Token);
        }
    }

    private void Keep(string? line) {
        if (line is not null) {
            output.Enqueue(line);
        }
    }

    private static string RelayAssembly() =>
        typeof(RelayProcess).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == relayAssemblyKey).Value!;

    private static int FreePort() {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int free = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return free;
    }

    private static string DotnetHost() => Path.GetFullPath(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(),
        "..",
        "..",
        "..",
        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
