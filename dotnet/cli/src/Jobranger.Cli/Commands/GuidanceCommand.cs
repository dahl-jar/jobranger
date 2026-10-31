using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceCommand : Command {
    public const string CommandName = "guidance";

    public GuidanceCommand(CliServices services)
        : this(services, EditServices.Create()) {
    }

    public GuidanceCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.GuidanceDescription) {
        Subcommands.Add(new GuidanceShowCommand(services, editServices.Environment));
        Subcommands.Add(new GuidanceEditCommand(services, editServices));
        Subcommands.Add(new GuidanceResetCommand(services, editServices));
    }
}
