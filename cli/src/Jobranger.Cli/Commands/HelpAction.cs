using System.CommandLine;
using System.CommandLine.Invocation;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

internal sealed class HelpAction(IAnsiConsole console, IMessages messages) : SynchronousCommandLineAction {
    private const string indent = "  ";
    private const string columnGap = "  ";
    private const string aliasSeparator = ", ";
    private const char optionPrefix = '-';

    public override int Invoke(ParseResult parseResult) {
        Command command = parseResult.CommandResult.Command;
        (string Names, string Description)[] options = [.. command.Options
            .Where(option => !option.Hidden)
            .Select(option => (Names(option), option.Description ?? string.Empty))];
        int width = options.Max(option => option.Names.Length);
        TextWriter output = console.Profile.Out.Writer;

        output.WriteLine(messages.DescriptionHeading);
        output.WriteLine(indent + command.Description);
        output.WriteLine();
        output.WriteLine(messages.UsageHeading);
        output.WriteLine($"{indent}{RootCommand.ProgramName} [{messages.OptionsPlaceholder}]");
        output.WriteLine();
        output.WriteLine(messages.OptionsHeading);
        foreach ((string names, string description) in options) {
            output.WriteLine($"{indent}{names.PadRight(width)}{columnGap}{description}");
        }
        return CliExitCodes.Ok;
    }

    private static string Names(Option option) =>
        string.Join(aliasSeparator, option.Aliases.Append(option.Name)
            .Where(name => name.StartsWith(optionPrefix))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.Ordinal));
}
