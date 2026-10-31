using System.CommandLine;
using System.Reflection;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RootCommandTests {
    private const string versionLine = @"^jobranger \d+\.\d+\.\d+$";
    private const string countOption = "--count";
    private const int longTokenLength = 200;

    private static readonly string englishHelp =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Help", "root.en.txt"));

    private static readonly string[] libraryWords =
        ["Description", "Usage", "Options", "Show", "Unrecognized", "Required", "Cannot parse"];

    [Fact]
    public async Task ShouldPrintAssemblyVersion_WhenVersionOptionGiven() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);

        int code = await root.RunAsync(["--version"], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Matches(versionLine, console.Output.TrimEnd());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData(null)]
    public async Task ShouldPrintHelp_WhenHelpOrNothingIsGiven(string? argument) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);

        int code = await root.RunAsync(argument is null ? [] : [argument], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Equal(englishHelp, console.Output);
    }

    [Theory]
    [InlineData("--no-such-option")]
    [InlineData("--version", "extra")]
    [InlineData("--first", "--second")]
    public async Task ShouldReturnUsageCode_WhenParseFails(params string[] args) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);

        int code = await root.RunAsync(args, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Empty(console.Output);
        Assert.All(args.Where(arg => arg != "--version"),
            arg => Assert.Contains(new EnglishMessages().UnknownArgument(arg), console.Errors, StringComparison.Ordinal));
        Assert.EndsWith(new EnglishMessages().HelpHint(RootCommand.ProgramName), console.Errors.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldKeepTokenOnOneLine_WhenUnknownTokenIsLong() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);
        string token = new('x', longTokenLength);

        await root.RunAsync([token], TestContext.Current.CancellationToken);

        Assert.Contains(new EnglishMessages().UnknownArgument(token), console.Errors.Split(Environment.NewLine));
    }

    [Fact]
    public async Task ShouldNameOption_WhenOptionValueIsInvalid() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);
        root.Options.Add(new Option<int>(countOption));

        int code = await root.RunAsync([countOption, "abc", "--nope"], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Contains(new EnglishMessages().InvalidOption(countOption), console.Errors, StringComparison.Ordinal);
        Assert.Contains(new EnglishMessages().UnknownArgument("--nope"), console.Errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportCommandError_WhenNoTokenOrOptionIsAtFault() {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);
        root.Validators.Add(result => result.AddError("refused"));

        int code = await root.RunAsync(["--version"], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Contains(new EnglishMessages().InvalidArguments, console.Errors, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--nope")]
    public async Task ShouldWriteNoEnglish_WhenLocaleIsNorwegian(string argument) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.Norwegian);

        await root.RunAsync([argument], TestContext.Current.CancellationToken);

        string written = console.Output + console.Errors;
        Assert.NotEmpty(written);
        Assert.DoesNotContain(EnglishTexts(argument), text => written.Contains(text, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ConfigCommand.CommandName)]
    [InlineData(GuidanceCommand.CommandName)]
    [InlineData(AboutCommand.CommandName)]
    [InlineData(ContextCommand.CommandName)]
    public async Task ShouldReturnUsageCode_WhenGroupHasNoSubcommand(string name) {
        using RecordingConsole console = new();
        RootCommand root = CreateRoot(console, Locales.English);

        int code = await root.RunAsync([name], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Contains(new EnglishMessages().InvalidArguments, console.Errors, StringComparison.Ordinal);
    }

    private static string[] EnglishTexts(string argument) {
        EnglishMessages english = new();
        IEnumerable<string> properties = typeof(EnglishMessages)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => (string)property.GetValue(english)!);
        return [
            .. properties,
            .. libraryWords,
            english.UnknownArgument(argument),
            english.HelpHint(RootCommand.ProgramName),
        ];
    }

    private static RootCommand CreateRoot(RecordingConsole console, string lang) =>
        new(CliServices.Create(
            TimeProvider.System, console.Console, console.ErrorConsole, Locales.EnvironmentWithoutConfig(lang)));
}
