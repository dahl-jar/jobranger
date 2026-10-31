using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceCommand : Command {
    public const string CommandName = "guidance";

    public GuidanceCommand(CliServices services)
        : this(services, EditSeams.System()) {
    }

    public GuidanceCommand(CliServices services, EditSeams seams)
        : base(CommandName, services.Messages.GuidanceDescription) {
        Subcommands.Add(new GuidanceShowCommand(services, seams.Environment));
        Subcommands.Add(new GuidanceEditCommand(services, seams));
        Subcommands.Add(new GuidanceResetCommand(services, seams));
    }
}
