using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class AboutCommand : Command {
    public const string CommandName = "about";

    public AboutCommand(CliServices services)
        : this(services, EditServices.Create()) {
    }

    public AboutCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.AboutDescription) {
        Subcommands.Add(new AboutEditCommand(services, editServices));
    }
}
