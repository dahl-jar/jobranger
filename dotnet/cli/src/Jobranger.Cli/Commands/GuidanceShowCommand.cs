using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceShowCommand : Command {
    public const string CommandName = "show";

    public GuidanceShowCommand(CliServices services, IDictionary environment)
        : base(CommandName, services.Messages.GuidanceShowDescription) {
        SetAction(_ => Run(services, environment));
    }

    private static int Run(CliServices services, IDictionary environment) {
        ILogger logger = services.Logs.CreateLogger(typeof(GuidanceShowCommand).FullName!);
        Paths paths = new(environment);
        return ReadFailures.PrintLines(
            () => Lines(paths, services.Messages),
            failure => LogEvents.SettingsReadFailed(logger, failure),
            paths,
            services.Messages,
            services.Console.Profile.Out.Writer);
    }

    private static IReadOnlyList<string> Lines(Paths paths, IMessages messages) {
        string path = paths.GuidancePath();
        GuidanceText guidance = MatchingEntry.LoadGuidance(path);
        return guidance.Text.Length == 0
            ? [messages.GuidanceShowDefault]
            : [messages.GuidanceShowEdited(path), guidance.Text];
    }
}
