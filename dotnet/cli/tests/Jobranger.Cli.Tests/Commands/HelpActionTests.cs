using System.CommandLine;
using System.CommandLine.Help;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using HelpAction = Jobranger.Cli.Commands.HelpAction;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class HelpActionTests {
    private const string helpOption = "--help";
    private const string leafName = "leaf";
    private const string secret = "secret";
    private const char pathSeparator = ' ';

    public static TheoryData<string> CommandPaths() {
        using RecordingConsole console = new();
        TheoryData<string> paths = [string.Empty];
        foreach ((string path, Command _) in Walk(CreateRoot(console, Locales.English))) {
            paths.Add(path);
        }
        return paths;
    }

    [Theory]
    [MemberData(nameof(CommandPaths))]
    public async Task ShouldPrintOwnHelp_WhenCommandIsInTree(string path) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);
        Command command = Find(root, path);
        string usage = $"{RootCommand.ProgramName} {path}".TrimEnd();

        int code = await root.RunAsync(HelpArgs(path), TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Empty(console.Errors);
        Assert.Contains($"  {usage} ", console.Output, StringComparison.Ordinal);
        Assert.All(command.Subcommands, child => Assert.Matches(RowPattern(child.Name), console.Output));
        Assert.All(command.Arguments, argument => Assert.Matches(RowPattern($"<{argument.Name}>"), console.Output));
    }

    [Theory]
    [MemberData(nameof(CommandPaths))]
    public void ShouldDescribeEverySymbol_WhenCommandIsInTree(string path) {
        using RecordingConsole console = new();
        Command command = Find(CreateRoot(console, Locales.English), path);

        Symbol[] symbols = [command, .. command.Arguments, .. command.Options];

        Assert.All(symbols, symbol => Assert.False(string.IsNullOrWhiteSpace(symbol.Description), symbol.Name));
    }

    [Theory]
    [InlineData("devices", Locales.English, "devices.en.txt")]
    [InlineData("devices revoke", Locales.English, "devices-revoke.en.txt")]
    [InlineData("devices revoke", Locales.Norwegian, "devices-revoke.nb.txt")]
    public async Task ShouldMatchGolden_WhenCommandHelpIsPrinted(string path, string lang, string golden) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, lang);
        string expected = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "TestData", "Help", golden), TestContext.Current.CancellationToken);

        int code = await root.RunAsync(HelpArgs(path), TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Equal(expected, console.Output);
    }

    [Fact]
    public async Task ShouldListPair_WhenRootHelpIsPrinted() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);
        string description = Regex.Escape(new EnglishMessages().PairDescription);

        await root.RunAsync([helpOption], TestContext.Current.CancellationToken);

        Assert.Matches($"(?m)^  {PairCommand.CommandName} +{description}$", console.Output);
    }

    [Fact]
    public async Task ShouldPrintHelp_WhenHelpFollowsUnknownToken() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);

        int code = await root.RunAsync(HelpArgs("devices nope"), TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Empty(console.Errors);
    }

    [Theory]
    [InlineData(0, 1, "[name]")]
    [InlineData(1, 3, "<name>...")]
    [InlineData(0, 3, "[name]...")]
    public async Task ShouldShowArgumentShape_WhenArityVaries(int minimum, int maximum, string token) {
        using RecordingConsole console = new();
        Command leaf = new(leafName) {
            new Argument<string[]>("name") { Arity = new ArgumentArity(minimum, maximum) },
        };
        ParseResult parsed = Help(console, leaf).Parse([leafName, helpOption]);

        await parsed.InvokeAsync(null, TestContext.Current.CancellationToken);

        Assert.Contains($"  {RootCommand.ProgramName} {leafName} {token} [", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLeaveOutSymbols_WhenMarkedHidden() {
        using RecordingConsole console = new();
        Command leaf = new(leafName) {
            new Argument<string>(secret) { Hidden = true },
            new Option<bool>($"--{secret}") { Hidden = true },
            new Command(secret) { Hidden = true },
        };
        ParseResult parsed = Help(console, leaf).Parse([leafName, helpOption]);

        await parsed.InvokeAsync(null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(secret, console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(new EnglishMessages().CommandPlaceholder, console.Output, StringComparison.Ordinal);
    }

    private static string RowPattern(string name) => $"(?m)^  {Regex.Escape(name)} ";

    private static IEnumerable<(string Path, Command Command)> Walk(Command parent, string prefix = "") {
        foreach (Command child in parent.Subcommands) {
            string path = prefix + child.Name;
            yield return (path, child);
            foreach ((string Path, Command Command) descendant in Walk(child, path + pathSeparator)) {
                yield return descendant;
            }
        }
    }

    private static Command Find(Command root, string path) =>
        path.Length == 0 ? root : Walk(root).Single(entry => entry.Path == path).Command;

    private static string[] HelpArgs(string path) =>
        [.. path.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries), helpOption];

    private static RootCommand CreateRoot(RecordingConsole console, string lang) =>
        new(CliServices.Create(
            TimeProvider.System, console.Console, console.ErrorConsole, Locales.EnvironmentWithoutConfig(lang)));

    private static System.CommandLine.RootCommand Help(RecordingConsole console, Command child) {
        System.CommandLine.RootCommand root = [child];
        root.Options.OfType<HelpOption>().Single().Action = new HelpAction(console.Console, new EnglishMessages());
        return root;
    }
}
