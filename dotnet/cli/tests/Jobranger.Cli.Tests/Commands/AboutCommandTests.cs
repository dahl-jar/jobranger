using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Messaging.Limits;
using Microsoft.Extensions.Logging.Abstractions;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class AboutCommandTests : IDisposable {
    private const string edit = "edit";
    private const string oldText = "Built a Java API.";
    private const string newText = "Built a C# CLI.";
    private const string kept = "Kept the previous text.\n";
    private const string keptNorwegian = "Beholdt den forrige teksten.\n";
    private const string aboutTooLong = "about: is longer than 4000 characters\n";
    private const string aboutTooLongNorwegian = "about: er lengre enn 4000 tegn\n";
    private const string contextTooLong = "context.yaml: is longer than 40000 characters\n";
    private const string contextTooLongNorwegian = "context.yaml: er lengre enn 40000 tegn\n";
    private const int fullDocs = 4;
    private const int fullDocLength = 10_000;

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public AboutCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldSaveAboutText_WhenEditRuns() {
        Save(new UserContext { About = oldText });
        string opened = string.Empty;
        FakeEditorProcess editor = new(file => {
            opened = File.ReadAllText(file);
            File.WriteAllText(file, newText);
            return 0;
        });

        int exit = await RunAsync(editor);

        Assert.Equal((CliExitCodes.Ok, $"✓ Saved {paths.ContextPath()}\n"), (exit, console.Output));
        Assert.Equal((oldText, newText), (opened, ProfilesEntry.LoadContext(paths.ContextPath()).About));
    }

    [Theory]
    [InlineData(Locales.English, false, aboutTooLong, EditFixtures.EditAgain, kept)]
    [InlineData(Locales.Norwegian, false, aboutTooLongNorwegian, EditFixtures.EditAgainNorwegian, keptNorwegian)]
    [InlineData(Locales.English, true, contextTooLong, EditFixtures.EditAgain, kept)]
    [InlineData(Locales.Norwegian, true, contextTooLongNorwegian, EditFixtures.EditAgainNorwegian, keptNorwegian)]
    public async Task ShouldKeepText_WhenLongEditIsDeclined(
        string lang, bool contextIsFull, string error, string prompt, string keptLine) {
        environment["LANG"] = lang;
        UserContext context = contextIsFull ? new UserContext { Docs = FullDocs() } : new UserContext { About = oldText };
        Save(context);
        string text = contextIsFull ? "x" : new string('x', MessageLimits.AboutLimit + 1);
        byte[] before = File.ReadAllBytes(paths.ContextPath());

        int exit = await RunAsync(EditFixtures.Writes(text), "n");

        Assert.Equal((CliExitCodes.Ok, error + prompt + keptLine), (exit, console.Output));
        Assert.Equal(before, File.ReadAllBytes(paths.ContextPath()));
    }

    [Fact]
    public async Task ShouldPrintErrors_WhenAboutFileIsBroken() {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ContextPath(), EditFixtures.BrokenContext);
        FakeEditorProcess editor = EditFixtures.Exits(0);

        int exit = await RunAsync(editor);

        Assert.Equal((CliExitCodes.Failure, EditFixtures.BrokenContextLine), (exit, console.Output));
        Assert.Empty(editor.Starts);
    }

    [Fact]
    public async Task ShouldPrintRecheckLine_WhenAboutEditChangesPrompt() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
        EditFixtures.AddAds(paths, 1, EditFixtures.CurrentPrompt(paths).Version);

        int exit = await RunAsync(EditFixtures.Writes(newText));

        Assert.Equal(
            (CliExitCodes.Ok, $"✓ Saved {paths.ContextPath()}\n  Rechecks 1 ad on the next run.\n"),
            (exit, console.Output));
    }

    private static IReadOnlyList<ContextDoc> FullDocs() =>
        [.. Enumerable.Range(1, fullDocs).Select(index => ContextFixtures.Doc with {
            Id = $"doc_{index}",
            Text = new string('x', fullDocLength),
        })];

    private void Save(UserContext context) => ProfilesEntry.SaveContext(paths.ContextPath(), context, NullLogger.Instance);

    private Task<int> RunAsync(FakeEditorProcess editor, params string?[] answers) {
        AboutCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor, answers));
        return EditFixtures.InvokeAsync(command, console, AboutCommand.CommandName, edit);
    }
}
