using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class AboutEditCommand : Command {
    public const string CommandName = "edit";

    private const string suffix = ".md";

    public AboutEditCommand(CliServices services, EditSeams seams)
        : base(CommandName, services.Messages.AboutEditDescription) {
        SetAction((_, cancellationToken) => RunAsync(services, seams, cancellationToken));
    }

    private static Task<int> RunAsync(CliServices services, EditSeams seams, CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(AboutEditCommand).FullName!);
        IMessages messages = services.Messages;
        Paths paths = new(seams.Environment);
        RechecksEntry rechecks = new(paths, seams.Time, logger);
        string path = paths.ContextPath();
        EditRequest request = new(
            () => ProfilesEntry.LoadContext(path).About,
            suffix,
            path,
            rechecks.SaveAbout,
            _ => messages.Saved(path),
            messages.AboutKept);
        return new EditSession(services, seams, logger).RunAsync(request, cancellationToken);
    }
}
