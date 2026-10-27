using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Spectre.Console;

namespace Jobranger.Cli;

internal static class Program {
    [UnsupportedOSPlatform("windows")]
    public static Task<int> Main(string[] args) {
        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings {
            Interactive = Console.IsErrorRedirected ? InteractionSupport.No : InteractionSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
        CliServices services = CliServices.Create(
            TimeProvider.System, AnsiConsole.Console, errorConsole, Environment.GetEnvironmentVariables());
        return new RootCommand(services).RunAsync(args, CancellationToken.None);
    }
}
