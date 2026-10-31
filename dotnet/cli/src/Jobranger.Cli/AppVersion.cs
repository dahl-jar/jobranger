using System.Reflection;

namespace Jobranger.Cli;

internal static class AppVersion {
    public static string Text { get; } = typeof(AppVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
