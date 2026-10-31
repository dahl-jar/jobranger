using System.Collections;
using System.ComponentModel;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class ConfigCommandTests : IDisposable {
    private const string show = "show";
    private const string edit = "edit";
    private const string unknownSetting = "nope: 1\n";
    private const string unknownSettingLine = "nope: is not a known setting\n";
    private const string unknownSettingLineNorwegian = "nope: er ikke en kjent innstilling\n";
    private const string kept = "Kept the previous config.\n";
    private const string keptNorwegian = "Beholdt det forrige oppsettet.\n";
    private const string missingLine = "No config yet. Set up with: jobranger init\n";
    private const string missingLineNorwegian = "Ingen oppsettsfil ennå. Sett opp med: jobranger init\n";
    private const string exitFailure = "exit";
    private const string missingEditor = "missing";
    private const string unreadableFile = "unreadable";
    private const string exitLine = "✕ The editor exited with an error. Nothing was saved.\n";
    private const string exitLineNorwegian = "✕ Redigeringsprogrammet avsluttet med en feil. Ingenting ble lagret.\n";
    private const string missingEditorLine = "✕ Could not start the editor vi. Set $VISUAL or $EDITOR.\n";
    private const string missingEditorLineNorwegian =
        "✕ Kunne ikke starte redigeringsprogrammet vi. Sett $VISUAL eller $EDITOR.\n";
    private const string tempFileLine = "✕ Could not read or write the temporary edit file. Nothing was saved.\n";
    private const string tempFileLineNorwegian =
        "✕ Kunne ikke lese eller skrive den midlertidige filen. Ingenting ble lagret.\n";

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly Paths paths;
    private readonly string validConfig = EditFixtures.Config(EditFixtures.NoModel);

    public ConfigCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldPrintPathAndFile_WhenConfigShowRuns() {
        string text = "# my comment\n" + validConfig;
        EditFixtures.WriteConfig(paths, text);

        int exit = await RunAsync(EditFixtures.Exits(0), show);

        Assert.Equal((CliExitCodes.Ok, $"# {paths.ConfigPath()}\n{text}"), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintErrors_WhenShownConfigIsInvalid() {
        EditFixtures.WriteConfig(paths, validConfig + unknownSetting);

        int exit = await RunAsync(EditFixtures.Exits(0), show);

        Assert.Equal(
            (CliExitCodes.Failure, unknownSettingLine, "settings.read_failed config_invalid"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Theory]
    [InlineData(show, Locales.English, missingLine)]
    [InlineData(show, Locales.Norwegian, missingLineNorwegian)]
    [InlineData(edit, Locales.English, missingLine)]
    [InlineData(edit, Locales.Norwegian, missingLineNorwegian)]
    public async Task ShouldPrintMissingLine_WhenConfigIsMissing(string command, string lang, string line) {
        environment["LANG"] = lang;
        FakeEditorProcess editor = EditFixtures.Exits(0);

        int exit = await RunAsync(editor, command);

        Assert.Equal((CliExitCodes.Failure, line), (exit, console.Output));
        Assert.Empty(editor.Starts);
    }

    [Fact]
    public async Task ShouldSaveEditedText_WhenConfigIsValid() {
        EditFixtures.WriteConfig(paths, validConfig);
        string text = "# my comment\n" + validConfig;

        int exit = await RunAsync(EditFixtures.Writes(text), edit);

        Assert.Equal((CliExitCodes.Ok, $"✓ Saved {paths.ConfigPath()}\n"), (exit, console.Output));
        Assert.Equal(text, File.ReadAllText(paths.ConfigPath()));
    }

    [Theory]
    [InlineData(Locales.English, "n", unknownSettingLine, EditFixtures.EditAgain, kept)]
    [InlineData(Locales.English, " NO ", unknownSettingLine, EditFixtures.EditAgain, kept)]
    [InlineData(Locales.Norwegian, "nei", unknownSettingLineNorwegian, EditFixtures.EditAgainNorwegian, keptNorwegian)]
    public async Task ShouldKeepFile_WhenInvalidEditIsDeclined(
        string lang, string answer, string error, string prompt, string keptLine) {
        environment["LANG"] = lang;
        EditFixtures.WriteConfig(paths, validConfig);

        int exit = await RunAsync(EditFixtures.Writes(validConfig + unknownSetting), [answer], edit);

        Assert.Equal((CliExitCodes.Ok, error + prompt + keptLine), (exit, console.Output));
        Assert.Equal(validConfig, File.ReadAllText(paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldReopenRejectedText_WhenUserEditsAgain() {
        EditFixtures.WriteConfig(paths, validConfig);
        List<string> opened = [];
        FakeEditorProcess editor = new(file => {
            opened.Add(File.ReadAllText(file));
            File.WriteAllText(file, opened.Count == 1 ? validConfig + unknownSetting : "# fixed\n" + validConfig);
            return 0;
        });

        int exit = await RunAsync(editor, [string.Empty], edit);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal([validConfig, validConfig + unknownSetting], opened);
        Assert.Equal("# fixed\n" + validConfig, File.ReadAllText(paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldStopAsking_WhenInputEnds() {
        EditFixtures.WriteConfig(paths, validConfig);
        int starts = 0;
        FakeEditorProcess editor = new(file => {
            starts++;
            File.WriteAllText(file, validConfig + unknownSetting);
            return starts == 1 ? 0 : 1;
        });

        int exit = await RunAsync(editor, edit);

        Assert.Equal((CliExitCodes.Ok, unknownSettingLine + EditFixtures.EditAgain + kept), (exit, console.Output));
        Assert.Single(editor.Starts);
    }

    [Fact]
    public async Task ShouldPrintRecheckLine_WhenConfigEditChangesPrompt() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
        AppConfig saved = ConfigFile.Load(paths.ConfigPath(), paths.Home());
        EditFixtures.AddAds(paths, 1, PromptSetup.Load(saved, paths).Version);
        string changed = validConfig.Replace("hide_masters: true", "hide_masters: false", StringComparison.Ordinal);

        int exit = await RunAsync(EditFixtures.Writes(changed), edit);

        Assert.Equal(
            (CliExitCodes.Ok, $"✓ Saved {paths.ConfigPath()}\n  Rechecks 1 ad on the next run.\n"),
            (exit, console.Output));
    }

    [Theory]
    [InlineData(exitFailure, Locales.English, exitLine, "editor_failed_exit")]
    [InlineData(exitFailure, Locales.Norwegian, exitLineNorwegian, "editor_failed_exit")]
    [InlineData(missingEditor, Locales.English, missingEditorLine, "editor_not_started")]
    [InlineData(missingEditor, Locales.Norwegian, missingEditorLineNorwegian, "editor_not_started")]
    [InlineData(unreadableFile, Locales.English, tempFileLine, "edit_file_failed")]
    [InlineData(unreadableFile, Locales.Norwegian, tempFileLineNorwegian, "edit_file_failed")]
    public async Task ShouldPrintEditorFailure_WhenEditorFails(string failure, string lang, string line, string code) {
        environment["LANG"] = lang;
        EditFixtures.WriteConfig(paths, validConfig);

        int exit = await RunAsync(Failing(failure), edit);

        Assert.Equal(
            (CliExitCodes.Failure, line, $"edit.failed {code}"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Equal(validConfig, File.ReadAllText(paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldPrintErrors_WhenEditedConfigFileIsBroken() {
        EditFixtures.WriteConfig(paths, string.Empty);
        File.WriteAllBytes(paths.ConfigPath(), Utf8Fixtures.WithInvalidByte("a", "b"));
        FakeEditorProcess editor = EditFixtures.Exits(0);

        int exit = await RunAsync(editor, edit);

        Assert.Equal(
            (CliExitCodes.Failure, "config.yaml: is not UTF-8 text\n", "edit.failed config_invalid"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Empty(editor.Starts);
    }

    [Fact]
    public async Task ShouldPrintWriteFailure_WhenFolderIsReadOnly() {
        EditFixtures.WriteConfig(paths, validConfig);
        FakeEditorProcess editor = new(file => {
            File.WriteAllText(file, "# edited\n" + validConfig);
            File.SetUnixFileMode(paths.ConfigDir(), EditFixtures.ReadOnlyDir);
            return 0;
        });

        int exit = await RunAsync(editor, edit);

        File.SetUnixFileMode(paths.ConfigDir(), FileModes.OwnerOnlyDir);
        Assert.Equal(
            (CliExitCodes.Failure, $"✕ Could not write {paths.ConfigPath()}.\n", "edit.failed save_failed"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Equal(validConfig, File.ReadAllText(paths.ConfigPath()));
    }

    private static FakeEditorProcess Failing(string failure) => failure switch {
        exitFailure => EditFixtures.Exits(1),
        missingEditor => new FakeEditorProcess(_ => throw new Win32Exception()),
        _ => new FakeEditorProcess(file => {
            File.Delete(file);
            return 0;
        }),
    };

    private Task<int> RunAsync(FakeEditorProcess editor, string subcommand) => RunAsync(editor, [], subcommand);

    private Task<int> RunAsync(FakeEditorProcess editor, string?[] answers, string subcommand) {
        ConfigCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.Seams(environment, editor, answers));
        return EditFixtures.InvokeAsync(command, console, ConfigCommand.CommandName, subcommand);
    }
}
