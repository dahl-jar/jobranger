using System.CommandLine;
using System.Reflection;
using System.Runtime.Versioning;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RootCommandTests {
    private const string versionLine = @"^jobranger \d+\.\d+\.\d+$";
    private const string countOption = "--count";
    private const string unknownOption = "--nope";
    private const string helpOption = "--help";
    private const string versionOption = "--version";
    private const int longTokenLength = 200;

    private static readonly string englishHelp =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Help", "root.en.txt"));

    private static readonly string[] libraryWords =
        ["Description", "Usage", "Options", "Show", "Unrecognized", "Required", "Cannot parse"];

    [Fact]
    public async Task ShouldPrintAssemblyVersion_WhenVersionOptionGiven() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);

        int code = await root.RunAsync([versionOption], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.Matches(versionLine, console.Output.TrimEnd());
    }

    [Theory]
    [InlineData(helpOption)]
    [InlineData(null)]
    public async Task ShouldPrintHelp_WhenHelpOrNothingIsGiven(string? argument) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);

        int code = await root.RunAsync(argument is null ? [] : [argument], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, code);
        Assert.StartsWith(englishHelp, console.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--no-such-option")]
    [InlineData(versionOption, "extra")]
    [InlineData("--first", "--second")]
    public async Task ShouldReturnUsageCode_WhenParseFails(params string[] args) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        EnglishMessages english = new();

        int code = await root.RunAsync(args, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Empty(console.Output);
        Assert.All(args.Where(arg => arg != versionOption),
            arg => Assert.Contains(english.UnknownArgument(arg), console.Errors, StringComparison.Ordinal));
        Assert.EndsWith(english.HelpHint(RootCommand.ProgramName), console.Errors.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldKeepTokenOnOneLine_WhenUnknownTokenIsLong() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        EnglishMessages english = new();
        string token = new('x', longTokenLength);

        await root.RunAsync([token], TestContext.Current.CancellationToken);

        Assert.Contains(english.UnknownArgument(token), console.Errors.Split(Environment.NewLine));
    }

    [Fact]
    public async Task ShouldNameOption_WhenOptionValueIsInvalid() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        EnglishMessages english = new();
        root.Options.Add(new Option<int>(countOption));

        int code = await root.RunAsync([countOption, "abc", unknownOption], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Contains(english.InvalidOption(countOption), console.Errors, StringComparison.Ordinal);
        Assert.Contains(english.UnknownArgument(unknownOption), console.Errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportCommandError_WhenNoTokenOrOptionIsAtFault() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.English);
        EnglishMessages english = new();
        root.Validators.Add(result => result.AddError("refused"));

        int code = await root.RunAsync([versionOption], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Usage, code);
        Assert.Contains(english.InvalidArguments, console.Errors, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HelpActionTests.CommandPaths), MemberType = typeof(HelpActionTests))]
    public async Task ShouldWriteNoEnglish_WhenCommandHelpIsNorwegian(string path) {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.Norwegian);

        await root.RunAsync(HelpActionTests.HelpArgs(path), TestContext.Current.CancellationToken);

        Assert.NotEmpty(console.Output);
        Assert.DoesNotContain(EnglishTexts(helpOption), text => console.Output.Contains(text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldWriteNoEnglish_WhenParseFailsInNorwegian() {
        using RecordingConsole console = new();
        RootCommand root = CommandFixtures.CreateRoot(console, Locales.Norwegian);

        await root.RunAsync([unknownOption], TestContext.Current.CancellationToken);

        string written = console.Output + console.Errors;
        Assert.NotEmpty(written);
        Assert.DoesNotContain(EnglishTexts(unknownOption), text => written.Contains(text, StringComparison.Ordinal));
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
}
