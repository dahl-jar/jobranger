using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class DevicesCommand : Command {
    public const string CommandName = "devices";

    public DevicesCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables(), TimeProvider.System, Link.OpenAsync) {
    }

    public DevicesCommand(
        CliServices services, IDictionary environment, TimeProvider time, LinkCommandRun.LinkOpener openLink)
        : base(CommandName, services.Messages.DevicesDescription) {
        Subcommands.Add(new DevicesListCommand(services, environment, time, openLink));
        Subcommands.Add(new DevicesRevokeCommand(services, environment, openLink));
    }
}
