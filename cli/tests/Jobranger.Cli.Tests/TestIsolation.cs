using Jobranger.Cli.Tests;

[assembly: AssemblyFixture(typeof(TestIsolation))]

namespace Jobranger.Cli.Tests;

public sealed class TestIsolation : IDisposable {
    private const string productPrefix = "JOBRANGER_";
    private const string rootPrefix = "jobranger-tests-";

    private static readonly string[] allowedSwitches = ["JOBRANGER_EMBEDDINGS_LIVE", "JOBRANGER_EMBEDDINGS_CACHE"];

    private static readonly string[] proxyVariables =
        ["HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy"];

    private static readonly (string Name, string Folder)[] userDirectories = [
        ("HOME", "home"),
        ("XDG_CONFIG_HOME", "config"),
        ("XDG_DATA_HOME", "data"),
        ("XDG_STATE_HOME", "state"),
        ("XDG_CACHE_HOME", "cache"),
        ("JOBRANGER_CONFIG_DIR", "config/jobranger"),
        ("JOBRANGER_DATA_DIR", "data/jobranger"),
        ("JOBRANGER_STATE_DIR", "state/jobranger"),
    ];

    private readonly Dictionary<string, string?> original = [];

    public TestIsolation() {
        Root = Directory.CreateTempSubdirectory(rootPrefix).FullName;
        string[] inherited = [.. Environment.GetEnvironmentVariables().Keys.OfType<string>()];
        foreach (string name in VariablesToClear(inherited)) {
            Set(name, null);
        }
        foreach ((string name, string folder) in userDirectories) {
            Set(name, Directory.CreateDirectory(Path.Combine(Root, folder)).FullName);
        }
    }

    public string Root { get; }

    public static string[] VariablesToClear(IEnumerable<string> inherited) =>
        [.. inherited.Where(name => IsClearedProductVariable(name) || proxyVariables.Contains(name, StringComparer.Ordinal))];

    public void Dispose() {
        foreach (KeyValuePair<string, string?> variable in original) {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
        Directory.Delete(Root, recursive: true);
    }

    private static bool IsClearedProductVariable(string name) =>
        name.StartsWith(productPrefix, StringComparison.Ordinal)
        && !allowedSwitches.Contains(name, StringComparer.Ordinal);

    private void Set(string name, string? value) {
        original.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }
}
