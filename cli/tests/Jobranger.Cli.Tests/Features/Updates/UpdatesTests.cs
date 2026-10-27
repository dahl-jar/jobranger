using System.Runtime.Versioning;
using Jobranger.Cli.Features.Updates;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Time.Testing;
using Spectre.Console;
using UpdatesEntry = Jobranger.Cli.Features.Updates.Updates;

namespace Jobranger.Cli.Tests.Features.Updates;

[UnsupportedOSPlatform("windows")]
public sealed class UpdatesTests : IDisposable {
    private const string methodVariable = "JOBRANGER_INSTALL_METHOD";
    private const string optOutVariable = "JOBRANGER_NO_UPDATE_CHECK";
    private const string processPath = "/usr/local/bin/jobranger";

    private static readonly string current = AppVersion.Text;

    private readonly TempDir temp = new();
    private readonly StringWriter errors = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

    public void Dispose() {
        errors.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData("homebrew", "Homebrew")]
    [InlineData("docker", "Docker")]
    [InlineData("archive", "Archive")]
    public async Task ShouldPrintUpgradeCommand_WhenNewerReleaseExists(string method, string expected) {
        UpdatesEntry updates = Create(new FakeGitHubHandler(GitHubReleases.All()), new() { [methodVariable] = method });

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"jobranger {GitHubReleases.Highest} is out (you have {current}). "
                + $"Upgrade: {InstallMethods.UpgradeCommand(Enum.Parse<InstallMethod>(expected))}\n",
            errors.ToString());
    }

    [Fact]
    public async Task ShouldPrintNorwegianNotice_WhenMessagesAreNorwegian() {
        UpdatesEntry updates = Create(
            new FakeGitHubHandler(GitHubReleases.All()), new() { [methodVariable] = "homebrew" }, new NorwegianMessages());

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"jobranger {GitHubReleases.Highest} er ute (du har {current}). "
                + $"Oppgrader: {InstallMethods.UpgradeCommand(InstallMethod.Homebrew)}\n",
            errors.ToString());
    }

    [Fact]
    public async Task ShouldPrintArchiveCommand_WhenProcessPathIsUnknown() {
        UpdatesEntry updates = Create(new FakeGitHubHandler(GitHubReleases.All()), [], path: null);

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.EndsWith(
            $"Upgrade: {InstallMethods.UpgradeCommand(InstallMethod.Archive)}\n", errors.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cli-v0.0.1")]
    [InlineData("cli-v{0}")]
    public async Task ShouldStaySilent_WhenLatestIsNotNewer(string tag) {
        UpdatesEntry updates = Create(new FakeGitHubHandler(GitHubReleases.Only(string.Format(null, tag, current))), []);

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal("", errors.ToString());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ShouldSkipCheck_WhenRunIsScheduledOrNotInteractive(bool scheduled, bool interactive) {
        FakeGitHubHandler handler = new(GitHubReleases.All());
        UpdatesEntry updates = Create(handler, [], new EnglishMessages(), interactive);

        await updates.MaybeNotifyAsync(scheduled, TestContext.Current.CancellationToken);

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("1.2.3+abc", "1.2.4", "1.2.3")]
    [InlineData("0.2.0-rc1", "0.2.1", "0.2.0")]
    public async Task ShouldPrintNotice_WhenCurrentVersionHasSuffix(string version, string latest, string shown) {
        UpdatesEntry updates = Create(
            new FakeGitHubHandler(GitHubReleases.Only($"cli-v{latest}")),
            new() { [methodVariable] = "homebrew" },
            version: version);

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"jobranger {latest} is out (you have {shown}). Upgrade: {InstallMethods.UpgradeCommand(InstallMethod.Homebrew)}\n",
            errors.ToString());
    }

    [Fact]
    public async Task ShouldStaySilent_WhenCurrentVersionIsUnparsable() {
        UpdatesEntry updates = Create(new FakeGitHubHandler(GitHubReleases.All()), [], version: "x");

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal("", errors.ToString());
    }

    [Theory]
    [InlineData(" False ", 1)]
    [InlineData("off", 1)]
    [InlineData("no", 1)]
    [InlineData("0", 1)]
    [InlineData(" ", 1)]
    [InlineData("1", 0)]
    public async Task ShouldReadOptOutAsSwitch_WhenOptOutVariableIsSet(string value, int requests) {
        FakeGitHubHandler handler = new(GitHubReleases.All());
        UpdatesEntry updates = Create(handler, new() { [optOutVariable] = value });

        await updates.MaybeNotifyAsync(scheduled: false, TestContext.Current.CancellationToken);

        Assert.Equal(requests, handler.RequestCount);
    }

    private UpdatesEntry Create(
        FakeGitHubHandler handler,
        Dictionary<string, string> environment,
        IMessages? messages = null,
        bool interactive = true,
        string? version = null,
        string? path = processPath) {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(errors),
            EnvironmentVariables = [],
        });
        return new UpdatesEntry(
            console, messages ?? new EnglishMessages(), environment, temp.File("version_check.json"), path,
            version ?? current, time,
            () => handler);
    }
}
