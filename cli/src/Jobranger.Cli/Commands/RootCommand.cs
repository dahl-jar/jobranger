using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Commands;

internal sealed class RootCommand : System.CommandLine.RootCommand {
    public const string ProgramName = "jobranger";

    private static readonly ParserConfiguration parserConfiguration = new() { ResponseFileTokenReplacer = null };

    private readonly CliServices services;

    public RootCommand(CliServices services) : base(services.Messages.RootDescription) {
        this.services = services;
        HelpAction help = new(services.Console, services.Messages);
        HelpOption helpOption = Options.OfType<HelpOption>().Single();
        helpOption.Description = services.Messages.HelpOptionDescription;
        helpOption.Action = help;
        VersionOption versionOption = Options.OfType<VersionOption>().Single();
        versionOption.Description = services.Messages.VersionOptionDescription;
        versionOption.Action = new VersionAction(services.Console);
        Action = help;
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken) {
        ParseResult parsed = Parse(args, parserConfiguration);
        if (parsed.Errors.Count > 0) {
            TextWriter errors = services.ErrorConsole.Profile.Out.Writer;
            foreach (string line in ParseErrorLines(parsed, services.Messages)) {
                await errors.WriteLineAsync(line).ConfigureAwait(false);
            }
            await errors.WriteLineAsync(services.Messages.HelpHint(ProgramName)).ConfigureAwait(false);
            return CliExitCodes.Usage;
        }
        InvocationConfiguration configuration = new() {
            Output = services.Console.Profile.Out.Writer,
            Error = services.ErrorConsole.Profile.Out.Writer,
        };
        return await parsed.InvokeAsync(configuration, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> ParseErrorLines(ParseResult parsed, IMessages messages) {
        IEnumerable<string> unknownTokens = parsed.UnmatchedTokens.Select(messages.UnknownArgument);
        IEnumerable<string> misusedOptions = parsed.Errors
            .Select(error => error.SymbolResult)
            .OfType<OptionResult>()
            .Select(result => messages.InvalidOption(result.Option.Name));
        int commandErrors = parsed.Errors.Count(error => error.SymbolResult is not OptionResult);
        IEnumerable<string> otherErrors = Enumerable.Repeat(
            messages.InvalidArguments, Math.Max(0, commandErrors - parsed.UnmatchedTokens.Count));
        return unknownTokens.Concat(misusedOptions).Concat(otherErrors);
    }
}
