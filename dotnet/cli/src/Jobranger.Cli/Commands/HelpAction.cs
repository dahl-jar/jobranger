using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

internal sealed class HelpAction(IAnsiConsole console, IMessages messages) : SynchronousCommandLineAction {
    private const string indent = "  ";
    private const string columnGap = "  ";
    private const string aliasSeparator = ", ";
    private const char optionPrefix = '-';
    private const char tokenSeparator = ' ';

    public override int Invoke(ParseResult parseResult) {
        Command[] path = CommandPath(parseResult.CommandResult);
        Command command = path[^1];
        TextWriter output = console.Profile.Out.Writer;

        output.WriteLine(messages.DescriptionHeading);
        output.WriteLine(indent + command.Description);
        output.WriteLine();
        output.WriteLine(messages.UsageHeading);
        output.WriteLine(indent + UsageLine(path));
        WriteSection(output, messages.ArgumentsHeading, ArgumentRows(command));
        WriteSection(output, messages.OptionsHeading, OptionRows(path));
        WriteSection(output, messages.CommandsHeading, CommandRows(command));
        return CliExitCodes.Ok;
    }

    private static Command[] CommandPath(CommandResult result) {
        Stack<Command> path = new();
        for (CommandResult? current = result; current is not null; current = current.Parent as CommandResult) {
            path.Push(current.Command);
        }
        return [.. path];
    }

    private string UsageLine(Command[] path) {
        Command command = path[^1];
        string commandToken = command.Action is null
            ? $"<{messages.CommandPlaceholder}>"
            : $"[{messages.CommandPlaceholder}]";
        string[] commandTokens = command.Subcommands.Count > 0 ? [commandToken] : [];
        return string.Join(tokenSeparator, [
            RootCommand.ProgramName,
            .. path.Skip(1).Select(step => step.Name),
            .. command.Arguments.Select(ArgumentToken),
            .. commandTokens,
            $"[{messages.OptionsPlaceholder}]",
        ]);
    }

    private static Row[] ArgumentRows(Command command) =>
        [.. command.Arguments.Select(argument => new Row(ArgumentToken(argument), argument.Description))];

    private static Row[] OptionRows(Command[] path) =>
        [.. path[^1].Options
            .Concat(path.SkipLast(1).SelectMany(ancestor => ancestor.Options).Where(option => option.Recursive))
            .Where(option => !option.Hidden)
            .Select(option => new Row(Names(option), option.Description))];

    private static Row[] CommandRows(Command command) =>
        [.. command.Subcommands.Select(child => new Row(child.Name, child.Description))];

    private static string ArgumentToken(Argument argument) => $"<{argument.Name}>";

    private static void WriteSection(TextWriter output, string heading, Row[] rows) {
        if (rows.Length == 0) {
            return;
        }
        int width = rows.Max(row => row.Name.Length);
        output.WriteLine();
        output.WriteLine(heading);
        foreach (Row row in rows) {
            output.WriteLine($"{indent}{row.Name.PadRight(width)}{columnGap}{row.Description}");
        }
    }

    private static string Names(Option option) =>
        string.Join(aliasSeparator, option.Aliases.Append(option.Name)
            .Where(name => name.StartsWith(optionPrefix))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.Ordinal));

    private readonly record struct Row(string Name, string? Description);
}
