using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

internal sealed class VersionAction(IAnsiConsole console) : SynchronousCommandLineAction {
    public override int Invoke(ParseResult parseResult) {
        console.WriteLine($"{RootCommand.ProgramName} {AppVersion.Text}");
        return CliExitCodes.Ok;
    }
}
