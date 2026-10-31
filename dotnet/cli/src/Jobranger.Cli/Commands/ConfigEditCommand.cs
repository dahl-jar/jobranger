using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ConfigEditCommand : Command {
    public const string CommandName = "edit";

    private const string suffix = ".yaml";

    public ConfigEditCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.ConfigEditDescription) {
        SetAction((_, cancellationToken) => EditSession.RunAsync(
            services,
            editServices,
            typeof(ConfigEditCommand),
            (paths, rechecks) => Request(services.Messages, paths, rechecks),
            cancellationToken));
    }

    private static EditRequest Request(IMessages messages, Paths paths, RechecksEntry rechecks) {
        string path = paths.ConfigPath();
        return new EditRequest(
            () => ConfigFile.ReadText(path),
            suffix,
            path,
            rechecks.SaveConfig,
            _ => messages.Saved(path),
            messages.ConfigKept);
    }
}
