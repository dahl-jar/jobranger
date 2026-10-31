using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceEditCommand : Command {
    public const string CommandName = "edit";

    private const string suffix = ".md";

    public GuidanceEditCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.GuidanceEditDescription) {
        SetAction((_, cancellationToken) => EditSession.RunAsync(
            services,
            editServices,
            typeof(GuidanceEditCommand),
            (paths, rechecks) => Request(services.Messages, paths, rechecks),
            cancellationToken));
    }

    private static EditRequest Request(IMessages messages, Paths paths, RechecksEntry rechecks) {
        string path = paths.GuidancePath();
        return new EditRequest(
            () => MatchingEntry.LoadGuidance(path).Text,
            suffix,
            path,
            rechecks.SaveGuidance,
            result => result.BackToDefault ? messages.GuidanceBackToDefault : messages.Saved(path),
            messages.GuidanceKept);
    }
}
