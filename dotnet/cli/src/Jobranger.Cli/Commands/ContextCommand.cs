using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ContextCommand : Command {
    public const string CommandName = "context";

    public ContextCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables()) {
    }

    public ContextCommand(CliServices services, IDictionary environment)
        : base(CommandName, services.Messages.ContextDescription) {
        Subcommands.Add(new ContextListCommand(services, environment));
    }
}
