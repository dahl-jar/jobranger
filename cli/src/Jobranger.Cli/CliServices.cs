using System.Collections;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli;

internal sealed class CliServices(IAnsiConsole console, IAnsiConsole errorConsole, IMessages messages) {
    public IAnsiConsole Console { get; } = console;

    public IAnsiConsole ErrorConsole { get; } = errorConsole;

    public IMessages Messages { get; } = messages;

    public static CliServices Create(IAnsiConsole console, IAnsiConsole errorConsole, IDictionary environment) =>
        new(console, errorConsole, Text.Messages.For(environment));
}
