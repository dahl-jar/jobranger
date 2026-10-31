using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class ContextCommandTests : IDisposable {
    private const string list = "list";
    private const string documentLines =
        "doc_cv_notes_md  CV notes.md  30 chars  added 2026-10-12\ndoc_emoji  Emoji notes  3 chars  added 2026-10-13\n";
    private const string documentLinesNorwegian =
        "doc_cv_notes_md  CV notes.md  30 tegn  lagt til 2026-10-12\ndoc_emoji  Emoji notes  3 tegn  lagt til 2026-10-13\n";
    private const string emptyLine = "No context documents. Add one with: jobranger context add <file>\n";
    private const string emptyLineNorwegian = "Ingen kontekstdokumenter. Legg til et med: jobranger context add <fil>\n";

    private static readonly ContextDoc emojiDoc = new("doc_emoji", "Emoji notes", "a\U0001F600b", "2026-10-13");

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public ContextCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(Locales.English, documentLines)]
    [InlineData(Locales.Norwegian, documentLinesNorwegian)]
    public async Task ShouldListDocuments_WhenContextHasDocuments(string lang, string lines) {
        environment["LANG"] = lang;
        UserContext context = new() { Docs = [ContextFixtures.Doc, emojiDoc] };
        ProfilesEntry.SaveContext(paths.ContextPath(), context, NullLogger.Instance);

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, lines), (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, emptyLine)]
    [InlineData(Locales.Norwegian, emptyLineNorwegian)]
    public async Task ShouldPrintEmptyLine_WhenContextHasNoDocuments(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintErrors_WhenContextFileIsBroken() {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ContextPath(), "- about\n");

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, "context.yaml: is not a section of settings\n"), (exit, console.Output));
    }

    private Task<int> RunAsync() {
        ContextCommand command = new(EditFixtures.Services(environment, console), environment);
        return EditFixtures.InvokeAsync(command, console, ContextCommand.CommandName, list);
    }
}
