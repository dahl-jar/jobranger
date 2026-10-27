using System.Collections;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli;

internal sealed class CliServices(
    IAnsiConsole console, IAnsiConsole errorConsole, IMessages messages) {
    public IAnsiConsole Console { get; } = console;

    public IAnsiConsole ErrorConsole { get; } = errorConsole;

    public IMessages Messages { get; } = messages;

    public static CliServices Create(IAnsiConsole console, IAnsiConsole errorConsole, IDictionary environment) {
        Paths paths = new(environment);
        IMessages messages = Text.Messages.For(ConfigFile.ReadLanguage(paths.ConfigPath()), environment);
        return new(console, errorConsole, messages);
    }
}
