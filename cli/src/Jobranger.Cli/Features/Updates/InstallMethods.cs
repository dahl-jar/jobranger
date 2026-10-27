using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Features.Updates;

internal static class InstallMethods {
    private const string methodVariable = "JOBRANGER_INSTALL_METHOD";
    private const string homebrewPrefixVariable = "HOMEBREW_PREFIX";
    private const string containerVariable = "DOTNET_RUNNING_IN_CONTAINER";
    private const string cellarFolder = "Cellar";
    private const string appName = "jobranger";
    private const string homebrewCommand = "brew upgrade jobranger";
    private const string dockerCommand = "docker pull ghcr.io/dahl-jar/jobranger-cli";
    private const string archiveCommand =
        "curl -fsSL https://github.com/dahl-jar/jobranger/releases/latest/download/install.sh | sh";

    private static readonly char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static InstallMethod Detect(string processPath, IDictionary environment) {
        if (Declared(environment) is InstallMethod declared) {
            return declared;
        }
        if (bool.TryParse(EnvironmentVariables.Read(environment, containerVariable), out bool container) && container) {
            return InstallMethod.Docker;
        }
        string? prefix = EnvironmentVariables.Read(environment, homebrewPrefixVariable);
        return IsHomebrew(processPath, prefix) || LinkTarget(processPath) is string target && IsHomebrew(target, prefix)
            ? InstallMethod.Homebrew
            : InstallMethod.Archive;
    }

    public static string UpgradeCommand(InstallMethod method) => method switch {
        InstallMethod.Homebrew => homebrewCommand,
        InstallMethod.Docker => dockerCommand,
        _ => archiveCommand,
    };

    private static InstallMethod? Declared(IDictionary environment) =>
        EnvironmentVariables.Read(environment, methodVariable) switch {
            "homebrew" => InstallMethod.Homebrew,
            "docker" => InstallMethod.Docker,
            "archive" => InstallMethod.Archive,
            _ => null,
        };

    private static bool IsHomebrew(string path, string? prefix) {
        string[] parts = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        bool inCellar = parts.Zip(parts.Skip(1)).Any(pair => pair is (cellarFolder, appName));
        return inCellar || prefix is not null && IsUnder(path, prefix);
    }

    private static bool IsUnder(string path, string root) =>
        Path.GetFullPath(path).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);

    private static string? LinkTarget(string path) {
        try {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return null;
        }
    }
}
