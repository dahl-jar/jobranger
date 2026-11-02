using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class EditLinkedFileTests : IDisposable {
    private const string edited = "new text\n";
    private const string targetName = "dotfile";
    private const string missingTarget = "missing/dotfile";

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public EditLinkedFileTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
        Directory.CreateDirectory(paths.ConfigDir());
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldWriteLinkTarget_WhenConfigIsLink() {
        string text = "# my comment\n" + EditFixtures.Config(EditFixtures.NoModel);
        string target = LinkTo(paths.ConfigPath(), EditFixtures.Config(EditFixtures.NoModel));

        await RunConfigAsync(EditFixtures.Writes(text));

        Assert.Equal(text, File.ReadAllText(target));
        Assert.NotNull(new FileInfo(paths.ConfigPath()).LinkTarget);
    }

    [Fact]
    public async Task ShouldWriteLinkTarget_WhenGuidanceIsLink() {
        string target = LinkTo(paths.GuidancePath(), "old\n");

        await RunGuidanceAsync(EditFixtures.Writes(edited), GuidanceEditCommand.CommandName);

        Assert.Equal(edited, File.ReadAllText(target));
        Assert.NotNull(new FileInfo(paths.GuidancePath()).LinkTarget);
    }

    [Fact]
    public async Task ShouldWriteLinkTarget_WhenContextIsLink() {
        string target = LinkTo(paths.ContextPath(), "about: old\ndocs: []\n");

        await RunAboutAsync(EditFixtures.Writes(edited));

        Assert.Contains("new text", File.ReadAllText(target), StringComparison.Ordinal);
        Assert.NotNull(new FileInfo(paths.ContextPath()).LinkTarget);
    }

    [Fact]
    public async Task ShouldKeepLink_WhenGuidanceIsReset() {
        string target = LinkTo(paths.GuidancePath(), "old\n");

        await RunGuidanceAsync(EditFixtures.Exits(0), GuidanceResetCommand.CommandName);

        Assert.Empty(File.ReadAllText(target));
        Assert.NotNull(new FileInfo(paths.GuidancePath()).LinkTarget);
    }

    [Fact]
    public async Task ShouldKeepTargetFolderMode_WhenFileIsLink() {
        string folder = temp.File("dotfiles");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, targetName);
        File.WriteAllText(target, "old\n");
        File.SetUnixFileMode(folder, FileModes.SharedDir);
        File.CreateSymbolicLink(paths.GuidancePath(), target);

        int exit = await RunGuidanceAsync(EditFixtures.Writes(edited), GuidanceEditCommand.CommandName);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal((edited, FileModes.SharedDir), (File.ReadAllText(target), File.GetUnixFileMode(folder)));
    }

    [Fact]
    public async Task ShouldPrintWriteFailure_WhenAboutLinkFolderIsMissing() {
        File.CreateSymbolicLink(paths.ContextPath(), temp.File(missingTarget));

        int exit = await RunAboutAsync(EditFixtures.Writes(edited));

        Assert.Equal((CliExitCodes.Failure, $"✕ Could not write {paths.ContextPath()}.\n"), (exit, console.Output));
        Assert.NotNull(new FileInfo(paths.ContextPath()).LinkTarget);
    }

    [Fact]
    public async Task ShouldPrintWriteFailure_WhenResetLinkFolderIsMissing() {
        File.CreateSymbolicLink(paths.GuidancePath(), temp.File(missingTarget));

        int exit = await RunGuidanceAsync(EditFixtures.Exits(0), GuidanceResetCommand.CommandName);

        Assert.Equal((CliExitCodes.Failure, $"✕ Could not write {paths.GuidancePath()}.\n"), (exit, console.Output));
        Assert.NotNull(new FileInfo(paths.GuidancePath()).LinkTarget);
    }

    private string LinkTo(string link, string content) {
        string target = temp.File(targetName);
        File.WriteAllText(target, content);
        File.CreateSymbolicLink(link, target);
        return target;
    }

    private Task<int> RunGuidanceAsync(FakeEditorProcess editor, string subcommand) {
        GuidanceCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor));
        return EditFixtures.InvokeAsync(command, console, GuidanceCommand.CommandName, subcommand);
    }

    private Task<int> RunAboutAsync(FakeEditorProcess editor) {
        AboutCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor));
        return EditFixtures.InvokeAsync(command, console, AboutCommand.CommandName, AboutEditCommand.CommandName);
    }

    private Task<int> RunConfigAsync(FakeEditorProcess editor) {
        ConfigCommand command = new(
            EditFixtures.Services(environment, console), EditFixtures.EditServices(environment, editor));
        return EditFixtures.InvokeAsync(command, console, ConfigCommand.CommandName, ConfigEditCommand.CommandName);
    }
}
