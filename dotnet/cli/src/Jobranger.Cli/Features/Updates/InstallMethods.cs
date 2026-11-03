using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Features.Updates;

internal static class InstallMethods {
    private const string methodVariable = "JOBRANGER_INSTALL_METHOD";
    private const string dockerValue = "docker";
    private const string dockerCommand = "docker pull ghcr.io/dahl-jar/jobranger/cli";
    private const string archiveCommand =
        "curl -fsSL https://github.com/dahl-jar/jobranger/releases/latest/download/install.sh | sh";

    public static InstallMethod Detect(IDictionary environment) =>
        EnvironmentVariables.Read(environment, methodVariable) == dockerValue
            ? InstallMethod.Docker
            : InstallMethod.Archive;

    public static string UpgradeCommand(InstallMethod method) => method switch {
        InstallMethod.Docker => dockerCommand,
        _ => archiveCommand,
    };
}
