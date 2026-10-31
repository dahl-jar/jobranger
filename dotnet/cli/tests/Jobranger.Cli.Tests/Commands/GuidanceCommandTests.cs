using System.Collections;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Messaging.Limits;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class GuidanceCommandTests : IDisposable {
    private const string show = "show";
    private const string edit = "edit";
    private const string reset = "reset";
    private const string oldText = "Prefer backend roles.\n";
    private const string newText = "Prefer data roles.\n";
    private const string defaultHeader = "# Matching guidance: default\n";
    private const string defaultHeaderNorwegian = "# Instrukser for vurderingen: standard\n";
    private const string editedHeader = "# Matching guidance: edited ({0})\n";
    private const string editedHeaderNorwegian = "# Instrukser for vurderingen: endret ({0})\n";
    private const string backToDefault = "✓ Back to the default guidance.\n";
    private const string backToDefaultNorwegian = "✓ Tilbake til standardinstruksene.\n";
    private const string saved = "✓ Saved {0}\n";
    private const string savedNorwegian = "✓ Lagret {0}\n";
    private const string writeFailed = "✕ Could not write {0}.\n";
    private const string writeFailedNorwegian = "✕ Kunne ikke skrive {0}.\n";
    private const string kept = "Kept the previous guidance.\n";
    private const string keptNorwegian = "Beholdt de forrige instruksene.\n";
    private const string tooLong = "guidance.md: is longer than 12000 characters\n";
    private const string tooLongNorwegian = "guidance.md: er lengre enn 12000 tegn\n";
    private const string oneAdLine = "  Rechecks 1 ad on the next run.\n";
    private const string pricedLine = @"\n  Rechecks 3 ads on the next run, up to about \$\d+\.\d{2}\.\n$";
    private const string editorFailed = "✕ The editor exited with an error. Nothing was saved.\n";
    private const string editorFailedNorwegian = "✕ Redigeringsprogrammet avsluttet med en feil. Ingenting ble lagret.\n";
    private const string savePrompt = "The editor exited with an error. Save your changes? (y/N) ";
    private const string savePromptNorwegian = "Redigeringsprogrammet avsluttet med en feil. Lagre endringene? (j/N) ";
    private const string noChanges = "No changes.\n";
    private const string noChangesNorwegian = "Ingen endringer.\n";
    private const string finalLineEnd = "\n";
    private const int failedExit = 1;
    private const int staleAds = 3;

    private static readonly DateTime pinnedTime = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public GuidanceCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(false, Locales.English, defaultHeader)]
    [InlineData(true, Locales.English, defaultHeader)]
    [InlineData(false, Locales.Norwegian, defaultHeaderNorwegian)]
    [InlineData(true, Locales.Norwegian, defaultHeaderNorwegian)]
    public async Task ShouldPrintDefaultHeader_WhenGuidanceIsDefault(bool emptyFile, string lang, string header) {
        environment["LANG"] = lang;
        if (emptyFile) {
            WriteGuidance(string.Empty);
        }

        int exit = await RunAsync(EditFixtures.Exits(0), show);

        Assert.Equal((CliExitCodes.Ok, header), (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, editedHeader)]
    [InlineData(Locales.Norwegian, editedHeaderNorwegian)]
    public async Task ShouldPrintEditedHeader_WhenGuidanceHasText(string lang, string header) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);

        int exit = await RunAsync(EditFixtures.Exits(0), show);

        Assert.Equal((CliExitCodes.Ok, string.Format(header, paths.GuidancePath()) + oldText), (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, saved)]
    [InlineData(Locales.Norwegian, savedNorwegian)]
    public async Task ShouldSaveGuidance_WhenEditHasText(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync(EditFixtures.Writes(newText), edit);

        Assert.Equal((CliExitCodes.Ok, string.Format(line, paths.GuidancePath())), (exit, console.Output));
        Assert.Equal(newText, File.ReadAllText(paths.GuidancePath()));
    }

    [Fact]
    public async Task ShouldLogFolderName_WhenFolderIsTightened() {
        WriteGuidance(oldText);
        File.SetUnixFileMode(paths.ConfigDir(), FileModes.SharedDir);

        await RunAsync(EditFixtures.Writes(newText), edit);

        IEnumerable<string?> tightened = LogLines.Read(CommandFixtures.LogPath(paths))
            .Where(line => line[LogFields.Event]?.ToString() == "paths.private_dir.tightened")
            .Select(line => line["dir"]?.ToString());
        Assert.Contains(Path.GetFileName(paths.ConfigDir()), tightened);
    }

    [Fact]
    public async Task ShouldLogSkippedRecheck_WhenConfigIsMissing() {
        int exit = await RunAsync(EditFixtures.Writes(newText), edit);

        JsonObject entry = LogLines.Read(CommandFixtures.LogPath(paths))[^1];
        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal(
            ("recheck.skipped", "ConfigMissingException"),
            (entry[LogFields.Event]?.ToString(), entry["error_type"]?.ToString()));
    }

    [Fact]
    public async Task ShouldOpenSavedGuidance_WhenEditStarts() {
        WriteGuidance(oldText);
        string opened = string.Empty;
        FakeEditorProcess editor = new(file => {
            opened = File.ReadAllText(file);
            return 0;
        });

        await RunAsync(editor, edit);

        Assert.Equal(oldText.TrimEnd(), opened);
    }

    [Theory]
    [InlineData(Locales.English, backToDefault)]
    [InlineData(Locales.Norwegian, backToDefaultNorwegian)]
    public async Task ShouldReturnToDefault_WhenEditIsEmpty(string lang, string line) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);

        int exit = await RunAsync(EditFixtures.Writes(string.Empty), edit);

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
        Assert.Empty(File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData(Locales.English, tooLong, EditFixtures.EditAgain, kept)]
    [InlineData(Locales.Norwegian, tooLongNorwegian, EditFixtures.EditAgainNorwegian, keptNorwegian)]
    public async Task ShouldKeepGuidance_WhenLongEditIsDeclined(string lang, string error, string prompt, string keptLine) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);
        string text = new('x', MessageLimits.GuidanceLimit + 1);

        int exit = await RunAsync(EditFixtures.Writes(text), ["n"], edit);

        Assert.Equal((CliExitCodes.Ok, error + prompt + keptLine), (exit, console.Output));
        Assert.Equal(oldText, File.ReadAllText(paths.GuidancePath()));
    }

    [Fact]
    public async Task ShouldPrintRecheckCost_WhenModelIsPriced() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.PricedModel);
        EditFixtures.AddAds(paths, staleAds, AdFixtures.OutdatedVersion);

        await RunAsync(EditFixtures.Writes(newText), edit);

        Assert.Matches(pricedLine, console.Output);
    }

    [Fact]
    public async Task ShouldPrintAdCountOnly_WhenModelHasNoPrice() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.UnpricedModel);
        EditFixtures.AddAds(paths, staleAds, AdFixtures.OutdatedVersion);

        await RunAsync(EditFixtures.Writes(newText), edit);

        Assert.Equal($"✓ Saved {paths.GuidancePath()}\n  Rechecks 3 ads on the next run.\n", console.Output);
    }

    [Fact]
    public async Task ShouldPrintNoRecheckLine_WhenNoAdIsOutdated() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.PricedModel);

        await RunAsync(EditFixtures.Writes(newText), edit);

        Assert.Equal($"✓ Saved {paths.GuidancePath()}\n", console.Output);
    }

    [Fact]
    public async Task ShouldReturnToDefault_WhenResetRuns() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
        WriteGuidance(oldText);
        EditFixtures.AddAds(paths, 1, AdFixtures.OutdatedVersion);

        int exit = await RunAsync(EditFixtures.Exits(0), reset);

        Assert.Equal((CliExitCodes.Ok, backToDefault + oneAdLine), (exit, console.Output));
        Assert.Empty(File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData(Locales.English, writeFailed)]
    [InlineData(Locales.Norwegian, writeFailedNorwegian)]
    public async Task ShouldPrintWriteFailure_WhenResetCannotWrite(string lang, string line) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);
        File.SetUnixFileMode(paths.ConfigDir(), EditFixtures.ReadOnlyDir);

        int exit = await RunAsync(EditFixtures.Exits(0), reset);

        File.SetUnixFileMode(paths.ConfigDir(), FileModes.OwnerOnlyDir);
        Assert.Equal(
            (CliExitCodes.Failure, string.Format(line, paths.GuidancePath()), "edit.failed save_failed"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Equal(oldText, File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData(show, "settings.read_failed config_invalid")]
    [InlineData(edit, "edit.failed config_invalid")]
    public async Task ShouldPrintErrors_WhenGuidanceFileIsBroken(string subcommand, string logged) {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllBytes(paths.GuidancePath(), Utf8Fixtures.WithInvalidByte("a", "b"));
        FakeEditorProcess editor = EditFixtures.Exits(0);

        int exit = await RunAsync(editor, subcommand);

        Assert.Equal(
            (CliExitCodes.Failure, "guidance.md: is not UTF-8 text\n", logged),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Empty(editor.Starts);
    }

    [Theory]
    [InlineData(Locales.English, "y", savePrompt, saved)]
    [InlineData(Locales.English, " YES ", savePrompt, saved)]
    [InlineData(Locales.Norwegian, "j", savePromptNorwegian, savedNorwegian)]
    public async Task ShouldSaveChanges_WhenFailedEditIsAccepted(string lang, string answer, string prompt, string line) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);

        int exit = await RunAsync(WritesThenFails(newText), [answer], edit);

        Assert.Equal((CliExitCodes.Ok, prompt + string.Format(line, paths.GuidancePath())), (exit, console.Output));
        Assert.Equal(newText, File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData(Locales.English, "n", savePrompt, editorFailed)]
    [InlineData(Locales.English, "", savePrompt, editorFailed)]
    [InlineData(Locales.English, null, savePrompt, editorFailed)]
    [InlineData(Locales.Norwegian, "nei", savePromptNorwegian, editorFailedNorwegian)]
    public async Task ShouldKeepGuidance_WhenFailedEditIsDeclined(string lang, string? answer, string prompt, string line) {
        environment["LANG"] = lang;
        WriteGuidance(oldText);

        int exit = await RunAsync(WritesThenFails(newText), [answer], edit);

        Assert.Equal(
            (CliExitCodes.Failure, prompt + line, "edit.failed editor_failed_exit"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
        Assert.Equal(oldText, File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(finalLineEnd)]
    public async Task ShouldAskNothing_WhenFailedEditLeavesTextUnchanged(string added) {
        WriteGuidance(oldText);
        FakeEditorProcess editor = new(file => {
            File.AppendAllText(file, added);
            return failedExit;
        });

        int exit = await RunAsync(editor, ["y"], edit);

        Assert.Equal((CliExitCodes.Failure, editorFailed), (exit, console.Output));
        Assert.Equal(oldText, File.ReadAllText(paths.GuidancePath()));
    }

    [Fact]
    public async Task ShouldAskNothing_WhenEditIsCancelled() {
        WriteGuidance(oldText);
        using CancellationTokenSource cancelled = new();
        FakeEditorProcess editor = new(file => {
            File.WriteAllText(file, newText);
            cancelled.Cancel();
            cancelled.Token.ThrowIfCancellationRequested();
            return 0;
        });
        GuidanceCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor, "y"));

        int exit = await EditFixtures.InvokeAsync(command, console, cancelled.Token, GuidanceCommand.CommandName, edit);

        Assert.Equal((CliExitCodes.Failure, editorFailed), (exit, console.Output));
        Assert.Equal(oldText, File.ReadAllText(paths.GuidancePath()));
    }

    [Theory]
    [InlineData(Locales.English, noChanges, "")]
    [InlineData(Locales.Norwegian, noChangesNorwegian, "")]
    [InlineData(Locales.English, noChanges, finalLineEnd)]
    public async Task ShouldPrintNoChanges_WhenEditLeavesTextUnchanged(string lang, string line, string added) {
        environment["LANG"] = lang;
        EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
        WriteGuidance(oldText);
        EditFixtures.AddAds(paths, 1, AdFixtures.OutdatedVersion);
        File.SetLastWriteTimeUtc(paths.GuidancePath(), pinnedTime);
        FakeEditorProcess editor = new(file => {
            File.AppendAllText(file, added);
            return 0;
        });

        int exit = await RunAsync(editor, edit);

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
        Assert.Equal(
            (oldText, pinnedTime),
            (File.ReadAllText(paths.GuidancePath()), File.GetLastWriteTimeUtc(paths.GuidancePath())));
    }

    private static FakeEditorProcess WritesThenFails(string text) => new(file => {
        File.WriteAllText(file, text);
        return failedExit;
    });

    private void WriteGuidance(string text) {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.GuidancePath(), text);
    }

    private Task<int> RunAsync(FakeEditorProcess editor, string subcommand) => RunAsync(editor, [], subcommand);

    private Task<int> RunAsync(FakeEditorProcess editor, string?[] answers, string subcommand) {
        GuidanceCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor, answers));
        return EditFixtures.InvokeAsync(command, console, GuidanceCommand.CommandName, subcommand);
    }
}
