using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Files;

internal sealed class Paths(IDictionary environment) {
    private const string appName = "jobranger";
    private const string homeVariable = "HOME";
    private const string configDirVariable = "JOBRANGER_CONFIG_DIR";
    private const string dataDirVariable = "JOBRANGER_DATA_DIR";
    private const string stateDirVariable = "JOBRANGER_STATE_DIR";
    private const string xdgConfigVariable = "XDG_CONFIG_HOME";
    private const string xdgDataVariable = "XDG_DATA_HOME";
    private const string xdgStateVariable = "XDG_STATE_HOME";
    private const string dotConfig = ".config";
    private const string macDataRoot = "Library/Application Support";
    private const string linuxDataRoot = ".local/share";
    private const string linuxStateRoot = ".local/state";
    private const string configFileName = "config.yaml";
    private const string profileFileName = "profile.yaml";
    private const string dbFileName = "jobranger.db";
    private const string runLockFileName = "run.lock";
    private const string versionCheckFileName = "version_check.json";
    private const string logDirName = "logs";

    public string Home() {
        if (Read(homeVariable) is string home) {
            return home;
        }
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return userProfile.Length > 0
            ? userProfile
            : throw new InvalidOperationException("home directory unknown, HOME is not set");
    }

    public string ConfigDir() =>
        ProductDir(configDirVariable) ?? XdgDir(xdgConfigVariable) ?? Path.Combine(Home(), dotConfig, appName);

    public string DataDir() =>
        ProductDir(dataDirVariable) ?? XdgDir(xdgDataVariable) ?? PlatformDir(linuxDataRoot);

    public string StateDir() =>
        ProductDir(stateDirVariable) ?? XdgDir(xdgStateVariable) ?? PlatformDir(linuxStateRoot);

    public string ConfigPath() => Path.Combine(ConfigDir(), configFileName);

    public string ProfilePath() => Path.Combine(ConfigDir(), profileFileName);

    public string DbPath() => Path.Combine(DataDir(), dbFileName);

    public string RunLockPath() => Path.Combine(StateDir(), runLockFileName);

    public string VersionCheckPath() => Path.Combine(StateDir(), versionCheckFileName);

    public string LogDir() => Path.Combine(StateDir(), logDirName);

    private string? ProductDir(string name) => Read(name) is string value ? Path.GetFullPath(value) : null;

    private string? XdgDir(string name) =>
        Read(name)?.Trim() is { Length: > 0 } value && Path.IsPathRooted(value) ? Path.Combine(value, appName) : null;

    private string PlatformDir(string unixRoot) {
        if (OperatingSystem.IsMacOS()) {
            return Path.Combine(Home(), macDataRoot, appName);
        }
        if (OperatingSystem.IsWindows()) {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        }
        return Path.Combine(Home(), unixRoot, appName);
    }

    private string? Read(string name) => EnvironmentVariables.Read(environment, name);
}
