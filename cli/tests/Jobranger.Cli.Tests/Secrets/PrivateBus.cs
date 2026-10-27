using System.Diagnostics;
using System.Reflection;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Tmds.DBus.Protocol;

namespace Jobranger.Cli.Tests.Secrets;

internal sealed class PrivateBus : IDisposable {
    private const string runtimeVariable = "XDG_RUNTIME_DIR";
    private const string configFileName = "bus.xml";
    private const string socketFileName = "bus";
    private const string cachedAddressField = "_sessionAddress";

    private static readonly FieldInfo cachedAddress =
        typeof(DBusAddress).GetField(cachedAddressField, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DBusAddress has no field name=" + cachedAddressField);

    private readonly string? originalAddress = Environment.GetEnvironmentVariable(SecretServiceStore.BusVariable);
    private readonly TempDir temp = new();
    private readonly Process daemon;
    private Process? keyring;

    public PrivateBus() {
        string configPath = temp.File(configFileName);
        File.WriteAllText(configPath, Config(temp.File(socketFileName)));
        daemon = Start("dbus-daemon", $"--config-file={configPath} --nofork --print-address=1", null);
        string address = daemon.StandardOutput.ReadLine()
            ?? throw new InvalidOperationException("dbus-daemon printed no address");
        UseAddress(address);
    }

    public void StartKeyring() {
        keyring = Start("gnome-keyring-daemon", "--start --foreground --components=secrets", temp.Path);
    }

    public void StopBus() {
        daemon.Kill();
        daemon.WaitForExit();
    }

    public void Dispose() {
        UseAddress(originalAddress);
        foreach (Process process in new[] { keyring, daemon }.OfType<Process>()) {
            if (!process.HasExited) {
                process.Kill();
                process.WaitForExit();
            }
            process.Dispose();
        }
        temp.Dispose();
    }

    private static string Config(string socketPath) => $$"""
        <busconfig>
          <type>session</type>
          <listen>unix:path={{socketPath}}</listen>
          <policy context="default">
            <allow send_destination="*" eavesdrop="true"/>
            <allow eavesdrop="true"/>
            <allow own="*"/>
          </policy>
        </busconfig>
        """;

    private static void UseAddress(string? address) {
        Environment.SetEnvironmentVariable(SecretServiceStore.BusVariable, address);
        cachedAddress.SetValue(null, null);
    }

    private static Process Start(string fileName, string arguments, string? runtimeDir) {
        ProcessStartInfo start = new(fileName, arguments) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        start.Environment[SecretServiceStore.BusVariable] =
            Environment.GetEnvironmentVariable(SecretServiceStore.BusVariable);
        if (runtimeDir is not null) {
            start.Environment[runtimeVariable] = runtimeDir;
        }
        return Process.Start(start) ?? throw new InvalidOperationException("process did not start name=" + fileName);
    }
}
