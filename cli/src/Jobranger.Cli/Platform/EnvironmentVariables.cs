using System.Collections;

namespace Jobranger.Cli.Platform;

internal static class EnvironmentVariables {
    public static string? Read(IDictionary environment, string name) =>
        environment[name] is string { Length: > 0 } value ? value : null;
}
