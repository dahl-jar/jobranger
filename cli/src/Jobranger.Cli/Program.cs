using Jobranger.Cli.Commands;
using Spectre.Console;

namespace Jobranger.Cli;

internal static class Program {
    public static Task<int> Main(string[] args) {
        IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(Console.Error),
        });
        CliServices services = CliServices.Create(
            AnsiConsole.Console, errorConsole, Environment.GetEnvironmentVariables());
        return new RootCommand(services).RunAsync(args, CancellationToken.None);
    }
}
