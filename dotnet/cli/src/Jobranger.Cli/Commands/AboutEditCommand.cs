using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class AboutEditCommand : Command {
    public const string CommandName = "edit";

    private const string suffix = ".md";

    public AboutEditCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.AboutEditDescription) {
        SetAction((_, cancellationToken) => EditSession.RunAsync(
            services,
            editServices,
            typeof(AboutEditCommand),
            (paths, rechecks) => Request(services.Messages, paths, rechecks),
            cancellationToken));
    }

    private static EditRequest Request(IMessages messages, Paths paths, RechecksEntry rechecks) {
        string path = paths.ContextPath();
        return new EditRequest(
            () => ProfilesEntry.LoadContext(path).About,
            suffix,
            path,
            rechecks.SaveAbout,
            _ => messages.Saved(path),
            messages.AboutKept);
    }
}
