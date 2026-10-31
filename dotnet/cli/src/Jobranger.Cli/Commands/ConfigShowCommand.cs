using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ConfigShowCommand : Command {
    public const string CommandName = "show";

    private const string headerPrefix = "# ";
    private const char newLine = '\n';

    public ConfigShowCommand(CliServices services, IDictionary environment)
        : base(CommandName, services.Messages.ConfigShowDescription) {
        SetAction(_ => Run(services, environment));
    }

    private static int Run(CliServices services, IDictionary environment) {
        ILogger logger = services.Logs.CreateLogger(typeof(ConfigShowCommand).FullName!);
        Paths paths = new(environment);
        return ReadFailures.PrintLines(
            () => Lines(paths),
            failure => LogEvents.SettingsReadFailed(logger, failure),
            paths,
            services.Messages,
            services.Console.Profile.Out.Writer);
    }

    private static IReadOnlyList<string> Lines(Paths paths) {
        string path = paths.ConfigPath();
        string text = ConfigFile.ReadText(path);
        ConfigFile.Parse(text, path, paths.Home());
        return [headerPrefix + path, text.TrimEnd(newLine)];
    }
}
