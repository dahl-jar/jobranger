using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Profiles;

[UnsupportedOSPlatform("windows")]
public sealed class ContextFileTests : IDisposable {
    private const string validDoc =
        "  - id: doc_one\n    name: notes.md\n    text: Notes.\n    added_at: 2026-10-12\n";

    private readonly TempDir temp = new();
    private readonly string path;

    public ContextFileTests() {
        path = temp.File("context.yaml");
    }

    public static TheoryData<string, string, string> BrokenFiles => new() {
        { "docs:\n" + validDoc + validDoc, "docs.1.id", nameof(ConfigProblem.DuplicateKey) },
        { "docs:\n" + validDoc.Replace("doc_one", "notes", StringComparison.Ordinal), "docs.0.id", nameof(ConfigProblem.NotDocId) },
        { "docs:\n" + validDoc.Replace("doc_one", "doc_", StringComparison.Ordinal), "docs.0.id", nameof(ConfigProblem.NotDocId) },
        { "docs:\n" + validDoc.Replace("notes.md", new string('n', 121), StringComparison.Ordinal), "docs.0.name", nameof(ConfigProblem.TooLong) },
        { "docs:\n" + validDoc.Replace("Notes.", new string('x', 12_001), StringComparison.Ordinal), "docs.0.text", nameof(ConfigProblem.TooLong) },
        { "about: " + new string('x', 4_001), "about", nameof(ConfigProblem.TooLong) },
        { "docs:\n" + Docs(4, new string('x', 10_000)) + validDoc, "context.yaml", nameof(ConfigProblem.TooLong) },
        { "docs:\n" + Docs(11, "Notes."), "docs", nameof(ConfigProblem.TooMany) },
        { "docs:\n" + validDoc.Replace("    added_at: 2026-10-12\n", string.Empty, StringComparison.Ordinal), "docs.0.added_at", nameof(ConfigProblem.Required) },
        { "docs:\n" + validDoc.Replace("notes.md", "''", StringComparison.Ordinal), "docs.0.name", nameof(ConfigProblem.Required) },
        { "docs:\n" + validDoc.Replace("2026-10-12", "2026-13-01", StringComparison.Ordinal), "docs.0.added_at", nameof(ConfigProblem.NotDate) },
        { "about: Notes.\nskills: []", "skills", nameof(ConfigProblem.UnknownKey) },
        { "- about", "context.yaml", nameof(ConfigProblem.NotSection) },
    };

    public static TheoryData<string> FilesAtLimit => new() {
        "docs:\n" + validDoc.Replace("notes.md", new string('n', 120), StringComparison.Ordinal),
        "docs:\n" + validDoc.Replace("Notes.", new string('x', 12_000), StringComparison.Ordinal),
        "about: " + new string('x', 4_000),
        "docs:\n" + Docs(10, "Notes."),
        "docs:\n" + Docs(4, new string('x', 10_000)),
    };

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldReturnEmptyContext_WhenFileIsMissing() {
        UserContext context = ProfilesEntry.LoadContext(path);

        Assert.Equal((string.Empty, 0), (context.About, context.Docs.Count));
    }

    [Fact]
    public void ShouldLoadSavedContextUnchanged_WhenSaved() {
        UserContext context = new() {
            About = "true",
            Docs = [
                new ContextDoc("doc_notes_md", "notes: 1.md", "Line one\n  Line two – æøå\n\n# not a comment", "2026-10-12"),
                new ContextDoc("doc_null", "null", "123", "2026-10-13"),
            ],
        };
        ProfilesEntry.SaveContext(path, context, NullLogger.Instance);

        UserContext loaded = ProfilesEntry.LoadContext(path);

        Assert.Equal(context.About, loaded.About);
        Assert.Equal(context.Docs, loaded.Docs);
    }

    [Fact]
    public void ShouldWriteOwnerOnlyFile_WhenSaving() {
        ProfilesEntry.SaveContext(path, new UserContext { About = "Notes." }, NullLogger.Instance);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldNotWriteFile_WhenSavedContextBreaksRule() {
        UserContext context = new() { About = new string('x', 4_001) };

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(
            () => ProfilesEntry.SaveContext(path, context, NullLogger.Instance));

        Assert.Equal(("about", false), (error.Errors[0].Location, File.Exists(path)));
    }

    [Theory]
    [MemberData(nameof(BrokenFiles))]
    public void ShouldRejectContextFile_WhenFieldBreaksRule(string text, string location, string problem) {
        File.WriteAllText(path, text);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.LoadContext(path));

        Assert.Equal(
            (path, location, Enum.Parse<ConfigProblem>(problem)),
            (error.Path, error.Errors[0].Location, error.Errors[0].Problem));
    }

    [Theory]
    [MemberData(nameof(FilesAtLimit))]
    public void ShouldLoadContextFile_WhenValueIsAtLimit(string text) {
        File.WriteAllText(path, text);

        Exception? error = Record.Exception(() => ProfilesEntry.LoadContext(path));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("docs:\n" + validDoc)]
    [InlineData("about: null\ndocs:\n" + validDoc)]
    public void ShouldLoadEmptyAbout_WhenAboutIsMissingOrNull(string text) {
        File.WriteAllText(path, text);

        UserContext context = ProfilesEntry.LoadContext(path);

        Assert.Equal(string.Empty, context.About);
        Assert.Equal(["doc_one"], ProfilesEntry.ContextIds(context));
    }

    private static string Docs(int count, string text) => string.Concat(Enumerable.Range(0, count).Select(index =>
        validDoc.Replace("doc_one", $"doc_{index}", StringComparison.Ordinal)
            .Replace("Notes.", text, StringComparison.Ordinal)));
}
