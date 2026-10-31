using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ConfigEditCommand : Command {
    public const string CommandName = "edit";

    private const string suffix = ".yaml";

    public ConfigEditCommand(CliServices services, EditSeams seams)
        : base(CommandName, services.Messages.ConfigEditDescription) {
        SetAction((_, cancellationToken) => RunAsync(services, seams, cancellationToken));
    }

    private static Task<int> RunAsync(CliServices services, EditSeams seams, CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(ConfigEditCommand).FullName!);
        IMessages messages = services.Messages;
        Paths paths = new(seams.Environment);
        RechecksEntry rechecks = new(paths, seams.Time, logger);
        string path = paths.ConfigPath();
        EditRequest request = new(
            () => ConfigFile.ReadText(path),
            suffix,
            path,
            rechecks.SaveConfig,
            _ => messages.Saved(path),
            messages.ConfigKept);
        return new EditSession(services, seams, logger).RunAsync(request, cancellationToken);
    }
}
