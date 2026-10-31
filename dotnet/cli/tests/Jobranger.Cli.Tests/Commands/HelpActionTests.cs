using System.CommandLine;
using System.CommandLine.Help;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
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
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        TheoryData<string> paths = [string.Empty];
        foreach ((string path, Command _) in Walk(root)) {
            paths.Add(path);
        }
        return paths;
    }

    [Theory]
    [MemberData(nameof(CommandPaths))]
    public async Task ShouldPrintOwnHelp_WhenHelpFollowsCommand(string path) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
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
    public void ShouldHaveDescriptions_WhenCommandIsRegistered(string path) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        Command command = Find(root, path);

        Symbol[] symbols = [command, .. command.Arguments, .. command.Options];

        Assert.All(symbols, symbol => Assert.False(string.IsNullOrWhiteSpace(symbol.Description), symbol.Name));
    }

    [Theory]
    [InlineData("devices", Locales.English, "devices.en.txt")]
    [InlineData("devices revoke", Locales.English, "devices-revoke.en.txt")]
    [InlineData("devices revoke", Locales.Norwegian, "devices-revoke.nb.txt")]
    public async Task ShouldPrintExactText_WhenCommandHelpIsRequested(string path, string lang, string golden) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, lang);
        string expected = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "TestData", "Help", golden), TestContext.Current.CancellationToken);

        int code = await root.RunAsync(HelpArgs(path), TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Equal(expected, console.Output);
    }

    [Fact]
    public async Task ShouldPrintHelp_WhenHelpFollowsUnknownToken() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);

        int code = await root.RunAsync(HelpArgs("devices nope"), TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Empty(console.Errors);
        Assert.Contains($"  {RootCommand.ProgramName} devices <", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLeaveOutOption_WhenMarkedHidden() {
        using RecordingConsole console = new();
        Command leaf = new(leafName) {
            new Option<bool>($"--{secret}") { Hidden = true },
        };
        EnglishMessages english = new();
        string usage = $"  {RootCommand.ProgramName} {leafName} [{english.OptionsPlaceholder}]";
        System.CommandLine.RootCommand help = CreateHelpRoot(console, leaf);
        ParseResult parsed = help.Parse([leafName, helpOption]);

        await parsed.InvokeAsync(null, TestContext.Current.CancellationToken);

        Assert.Contains(usage, console.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, console.Output, StringComparison.Ordinal);
    }

    internal static string[] HelpArgs(string path) =>
        [.. path.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries), helpOption];

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

    private static System.CommandLine.RootCommand CreateHelpRoot(RecordingConsole console, Command child) {
        System.CommandLine.RootCommand root = [child];
        root.Options.OfType<HelpOption>().Single().Action = new HelpAction(console.Console, new EnglishMessages());
        return root;
    }
}
