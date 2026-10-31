using System.Collections;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class InitPtyTests : IDisposable {
    private const string ctrlC = "\x03";
    private const string enter = "\r";
    private const string space = " ";
    private const string downArrow = "\x1b[B";
    private const string hideCursor = "\x1b[?25l";
    private const string showCursor = "\x1b[?25h";
    private const string secretKey = "sk-fake-echo-1234";
    private const string keyStart = "sk-fake";
    private const string bracketTags = """{"models":[{"name":"llama3 [beta]"}]}""";

    private static readonly EnglishMessages english = new();

    private readonly TempDir temp = new();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public InitPtyTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldExit130_WhenCtrlCArrivesAtConfirm() {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ConfigPath(), InitFixtures.OldConfig);
        byte[] old = File.ReadAllBytes(paths.ConfigPath());
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await pty.WaitForAsync("A config already");

        await pty.SendAsync(ctrlC);
        await pty.WaitForExitAsync();

        Assert.Equal(RunCommandFixtures.Interrupted, pty.ExitCode);
        Assert.Contains(english.InitCancelled, pty.Transcript, StringComparison.Ordinal);
        Assert.Equal(old, File.ReadAllBytes(paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldShowCursor_WhenCtrlCArrivesAtSelect() {
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await pty.WaitForAsync(english.InitProvider);

        await pty.SendAsync(ctrlC);
        await pty.WaitForExitAsync();

        string transcript = pty.Transcript;
        Assert.Equal(RunCommandFixtures.Interrupted, pty.ExitCode);
        Assert.InRange(
            transcript.LastIndexOf(hideCursor, StringComparison.Ordinal),
            0,
            transcript.LastIndexOf(showCursor, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldNotEchoKey_WhenKeyIsTyped() {
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await pty.WaitForAsync(english.InitProvider);
        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitApiKey);

        await pty.SendAsync(secretKey);
        await pty.SendAsync(ctrlC);
        await pty.WaitForExitAsync();

        Assert.Equal(RunCommandFixtures.Interrupted, pty.ExitCode);
        Assert.DoesNotContain(keyStart, pty.Transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("*", pty.Transcript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldExit130_WhenCtrlCArrivesAtCheckbox() {
        await using RawHttpServer ollama = new(TagsReply(InitFixtures.OllamaList));
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await ReachRolesPromptAsync(pty, ollama);

        await pty.SendAsync(ctrlC);
        await pty.WaitForExitAsync();

        Assert.Equal(RunCommandFixtures.Interrupted, pty.ExitCode);
        Assert.False(File.Exists(paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldKeepBrackets_WhenModelNameHasThem() {
        await using RawHttpServer ollama = new(TagsReply(bracketTags));
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await ReachModelPromptAsync(pty, ollama);
        await pty.WaitForAsync("llama3 [beta]");

        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitProfilePath);

        await pty.SendAsync(ctrlC);
        await pty.WaitForExitAsync();
        Assert.Equal(RunCommandFixtures.Interrupted, pty.ExitCode);
    }

    [Fact]
    public async Task ShouldUseDefaultPath_WhenEnterIsPressedAtProfile() {
        await using RawHttpServer ollama = new(TagsReply(InitFixtures.OllamaList));
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ProfilePath(), InitFixtures.ProfileBody);
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await ReachModelPromptAsync(pty, ollama);
        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitProfilePath);

        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitRoles);
    }

    [Fact]
    public async Task ShouldStartOver_WhenNorwegianUserAnswersJ() {
        NorwegianMessages norwegian = new();
        environment["LANG"] = Locales.Norwegian;
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ConfigPath(), InitFixtures.OldConfig);
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await pty.WaitForAsync("Det finnes allerede");

        await pty.SendAsync("j" + enter);
        await pty.WaitForAsync(norwegian.InitProvider);
    }

    [Fact]
    public async Task ShouldKeepPreChecked_WhenEnterIsPressedAtLevels() {
        await using RawHttpServer ollama = new(TagsReply(InitFixtures.OllamaList));
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);
        await ReachRolesPromptAsync(pty, ollama);
        await pty.SendAsync(space + enter);
        await pty.WaitForAsync(english.InitLevels);

        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitYears);
    }

    [Fact]
    public async Task ShouldPrintOneLine_WhenTermIsDumb() {
        environment["TERM"] = "dumb";
        await using PtyProcess pty = PtyProcess.Start(environment, InitCommand.CommandName);

        await pty.WaitForExitAsync();

        Assert.Equal(CliExitCodes.Failure, pty.ExitCode);
        Assert.Contains(english.InitNeedsTerminal, pty.Transcript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintOneLine_WhenOutputIsRedirected() {
        string outputFile = temp.File("output.txt");
        await using PtyProcess pty = PtyProcess.StartWithOutputFile(environment, outputFile, InitCommand.CommandName);

        await pty.WaitForExitAsync();

        Assert.Equal(CliExitCodes.Failure, pty.ExitCode);
        Assert.Equal(InitHarness.Lines(english.InitNeedsTerminal), File.ReadAllText(outputFile));
    }

    private static async Task ReachModelPromptAsync(PtyProcess pty, RawHttpServer ollama) {
        await pty.WaitForAsync(english.InitProvider);
        await pty.SendAsync(downArrow + downArrow + downArrow + enter);
        await pty.WaitForAsync(english.InitOllamaUrl);
        await pty.SendAsync(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{ollama.Port}{enter}"));
        await pty.WaitForAsync(english.InitModel);
    }

    private static string TagsReply(string body) => string.Create(
        CultureInfo.InvariantCulture,
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");

    private async Task ReachRolesPromptAsync(PtyProcess pty, RawHttpServer ollama) {
        string profile = InitFixtures.WriteProfile(temp);
        await ReachModelPromptAsync(pty, ollama);
        await pty.SendAsync(enter);
        await pty.WaitForAsync(english.InitProfilePath);
        await pty.SendAsync(profile + enter);
        await pty.WaitForAsync(english.InitRoles);
    }
}
