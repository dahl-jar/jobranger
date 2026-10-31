using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ConfigCommand : Command {
    public const string CommandName = "config";

    public ConfigCommand(CliServices services)
        : this(services, EditServices.Create()) {
    }

    public ConfigCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.ConfigDescription) {
        Subcommands.Add(new ConfigShowCommand(services, editServices.Environment));
        Subcommands.Add(new ConfigEditCommand(services, editServices));
    }
}
