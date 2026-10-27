using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

internal sealed class VersionAction(IAnsiConsole console) : SynchronousCommandLineAction {
    private static readonly string version = typeof(VersionAction).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    public override int Invoke(ParseResult parseResult) {
        console.WriteLine($"{RootCommand.ProgramName} {version}");
        return CliExitCodes.Ok;
    }
}
