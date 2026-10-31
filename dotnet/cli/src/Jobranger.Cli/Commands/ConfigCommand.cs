using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ConfigCommand : Command {
    public const string CommandName = "config";

    public ConfigCommand(CliServices services)
        : this(services, EditSeams.System()) {
    }

    public ConfigCommand(CliServices services, EditSeams seams)
        : base(CommandName, services.Messages.ConfigDescription) {
        Subcommands.Add(new ConfigShowCommand(services, seams.Environment));
        Subcommands.Add(new ConfigEditCommand(services, seams));
    }
}
