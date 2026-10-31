using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ContextListCommand : Command {
    public const string CommandName = "list";

    public ContextListCommand(CliServices services, IDictionary environment)
        : base(CommandName, services.Messages.ContextListDescription) {
        SetAction(_ => Run(services, environment));
    }

    private static int Run(CliServices services, IDictionary environment) {
        ILogger logger = services.Logs.CreateLogger(typeof(ContextListCommand).FullName!);
        Paths paths = new(environment);
        return ReadFailures.PrintLines(
            () => Lines(paths, services.Messages),
            failure => LogEvents.SettingsReadFailed(logger, failure),
            paths,
            services.Messages,
            services.Console.Profile.Out.Writer);
    }

    private static IReadOnlyList<string> Lines(Paths paths, IMessages messages) {
        UserContext context = ProfilesEntry.LoadContext(paths.ContextPath());
        return context.Docs.Count == 0
            ? [messages.ContextListEmpty]
            : [.. context.Docs.Select(doc => messages.ContextListLine(doc.Id, doc.Name, CodePoints.Count(doc.Text), doc.AddedAt))];
    }
}
