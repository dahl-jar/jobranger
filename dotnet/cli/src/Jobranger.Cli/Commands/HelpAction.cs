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
    private const string repeatMark = "...";
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
        string[] commandTokens = VisibleCommands(command).Any() ? [commandToken] : [];
        return string.Join(tokenSeparator, [
            RootCommand.ProgramName,
            .. path.Skip(1).Select(step => step.Name),
            .. VisibleArguments(command).Select(ArgumentToken),
            .. commandTokens,
            $"[{messages.OptionsPlaceholder}]",
        ]);
    }

    private static (string Name, string Description)[] ArgumentRows(Command command) =>
        [.. VisibleArguments(command).Select(argument => (ArgumentToken(argument), argument.Description ?? string.Empty))];

    private static (string Name, string Description)[] OptionRows(Command[] path) =>
        [.. path[^1].Options
            .Concat(path.SkipLast(1).SelectMany(ancestor => ancestor.Options).Where(option => option.Recursive))
            .Where(option => !option.Hidden)
            .Select(option => (Names(option), option.Description ?? string.Empty))];

    private static (string Name, string Description)[] CommandRows(Command command) =>
        [.. VisibleCommands(command).Select(child => (child.Name, child.Description ?? string.Empty))];

    private static IEnumerable<Argument> VisibleArguments(Command command) =>
        command.Arguments.Where(argument => !argument.Hidden);

    private static IEnumerable<Command> VisibleCommands(Command command) =>
        command.Subcommands.Where(child => !child.Hidden);

    private static string ArgumentToken(Argument argument) {
        string token = argument.Arity.MinimumNumberOfValues == 0 ? $"[{argument.Name}]" : $"<{argument.Name}>";
        return argument.Arity.MaximumNumberOfValues > 1 ? token + repeatMark : token;
    }

    private static void WriteSection(TextWriter output, string heading, (string Name, string Description)[] rows) {
        if (rows.Length == 0) {
            return;
        }
        int width = rows.Max(row => row.Name.Length);
        output.WriteLine();
        output.WriteLine(heading);
        foreach ((string name, string description) in rows) {
            output.WriteLine($"{indent}{name.PadRight(width)}{columnGap}{description}");
        }
    }

    private static string Names(Option option) =>
        string.Join(aliasSeparator, option.Aliases.Append(option.Name)
            .Where(name => name.StartsWith(optionPrefix))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.Ordinal));
}
