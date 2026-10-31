using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Spectre.Console;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using LinkEntry = Jobranger.Cli.Features.Link.Link;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class ReportCommandTests : IDisposable {
    private const string juniorLabel = "junior";
    private const string seniorLabel = "senior";
    private const string noMatches = "No matches yet. Run one with: jobranger run\n";
    private const string noMatchesNorwegian = "Ingen treff ennå. Start en kjøring med: jobranger run\n";
    private const string strongLine = "  Strong fit    Software Developer     Org One AS        Bergen      12 Oct";
    private const string applySoonTitle = "Apply this week";
    private const string partialReport =
        "New today\n  Partial fit   Backend Developer      Org Two AS        Oslo        no date\n";
    private const string englishReport =
        "Apply this week\n"
        + "  Strong fit    Software Developer     Org One AS        Bergen      12 Oct\n"
        + "\n"
        + "New today\n"
        + "  Partial fit   Backend Developer      Org Two AS        Oslo        no date\n"
        + "\n"
        + "1 hidden: 1 senior\n";
    private const string norwegianReport =
        "Søk denne uken\n"
        + "  Passer godt   Software Developer     Org One AS        Bergen      12. okt\n"
        + "\n"
        + "Nye i dag\n"
        + "  Passer delvis Backend Developer      Org Two AS        Oslo        ingen dato\n"
        + "\n"
        + "1 skjult: 1 senior\n";
    private const string dataDirVariable = "JOBRANGER_DATA_DIR";
    private const int corruptBytes = 4096;
    private const double requiredYears = 5;
    private const int testTimeoutMs = 10_000;

    private static readonly Ad strongAd = AdFixtures.Numbered(1) with {
        Title = "Software Developer",
        DisplayTitle = "Software Developer",
        Company = "Org One AS",
        Location = "Bergen",
        Deadline = "2026-10-12",
        Url = "https://jobs.example.test/ad/1",
    };

    private static readonly Ad partialAd = AdFixtures.Numbered(2) with {
        Title = "Backend Developer",
        DisplayTitle = "Backend Developer",
        Company = "Org Two AS",
        Location = "Oslo",
        Url = "https://jobs.example.test/ad/2",
    };

    private static readonly Ad seniorAd = AdFixtures.Numbered(3) with {
        Title = "Senior Developer",
        DisplayTitle = "Senior Developer",
        Company = "Org Three AS",
        Location = "Bergen",
        Url = "https://jobs.example.test/ad/3",
    };

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider clock = AdFixtures.Clock();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public ReportCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(Locales.English, englishReport)]
    [InlineData(Locales.Norwegian, norwegianReport)]
    public async Task ShouldPrintReport_WhenMatchesExist(string lang, string report) {
        environment["LANG"] = lang;
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        AddAd(partialAd, juniorLabel, Fit.Stretch, version);
        AddAd(seniorAd, seniorLabel, Fit.Strong, version);
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, report), (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, noMatches)]
    [InlineData(Locales.Norwegian, noMatchesNorwegian)]
    public async Task ShouldPrintNoMatches_WhenNoRunExists(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintNoMatches_WhenOnlyHiddenAdsExist() {
        string version = SaveConfig();
        AddAd(seniorAd, seniorLabel, Fit.Strong, version);
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, noMatches), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldLeaveOutBlock_WhenBlockHasNoMatches() {
        string version = SaveConfig();
        AddAd(partialAd, juniorLabel, Fit.Stretch, version);
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, partialReport), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldListReportedAd_WhenLastRunReportedIt() {
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        StartRun();
        WithStore(store => store.MarkReported([strongAd.Id], AdFixtures.Now.AddMinutes(1)));

        await RunAsync();

        Assert.Contains(strongLine, console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLeaveOutReportedAd_WhenEarlierRunReportedIt() {
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        StartRun();
        WithStore(store => store.MarkReported([strongAd.Id], AdFixtures.Now.AddDays(-1)));

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Fact]
    public async Task ShouldLeaveOutReportedAd_WhenAdIsClosed() {
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        StartRun();
        WithStore(store => {
            store.MarkReported([strongAd.Id], AdFixtures.Now.AddMinutes(1));
            store.SetActive(strongAd.Id, false);
        });

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Fact]
    public async Task ShouldLeaveOutReportedAd_WhenUserApplied() {
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        StartRun();
        WithStore(store => {
            store.MarkReported([strongAd.Id], AdFixtures.Now.AddMinutes(1));
            store.MarkAd(strongAd.Id, AdAction.Applied, AdFixtures.Now.AddMinutes(1));
        });

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Fact]
    public async Task ShouldListDuplicateOnce_WhenTwoSourcesHaveAd() {
        string version = SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, version);
        AddAd(
            strongAd with { Id = "nav:1", Source = "nav", SourceId = "1", Url = "https://nav.example.test/ad/1" },
            juniorLabel,
            Fit.Strong,
            version);
        StartRun();

        await RunAsync();

        Assert.Equal($"{applySoonTitle}\n{strongLine}\n", console.Output);
    }

    [Fact]
    public async Task ShouldLeaveOutAd_WhenAssessmentVersionIsOld() {
        SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, AdFixtures.OutdatedVersion);
        StartRun();

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Fact]
    public async Task ShouldLeaveOutHiddenAd_WhenFirstSeenBeforeLastRun() {
        string version = SaveConfig();
        AddAd(partialAd, juniorLabel, Fit.Stretch, version);
        AddAd(seniorAd, seniorLabel, Fit.Strong, version, new FakeTimeProvider(AdFixtures.Now.AddDays(-1)));
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, partialReport), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldLeaveOutReportedAd_WhenAssessmentVersionIsOld() {
        SaveConfig();
        AddAd(strongAd, juniorLabel, Fit.Strong, AdFixtures.OutdatedVersion);
        StartRun();
        WithStore(store => store.MarkReported([strongAd.Id], AdFixtures.Now.AddMinutes(1)));

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Fact]
    public async Task ShouldLeaveOutHiddenCount_WhenUserAskedToSeeAd() {
        string version = SaveConfig();
        AddAd(seniorAd, seniorLabel, Fit.Strong, version);
        WithStore(store => store.MarkAd(seniorAd.Id, AdAction.ShowAnyway, AdFixtures.Now));
        StartRun();

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Ok, "New today\n  Strong fit    Senior Developer       Org Three AS      Bergen      no date\n"),
            (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintYearCap_WhenAdNeedsMoreYears() {
        string version = SaveConfig();
        AddAd(partialAd, juniorLabel, Fit.Stretch, version);
        AddAd(seniorAd, juniorLabel, Fit.Strong, version, years: requiredYears);
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, $"{partialReport}\n1 hidden: 1 need 1+ yrs\n"), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldLeaveOutAd_WhenDeadlinePassedInLocalTime() {
        clock.SetUtcNow(new DateTimeOffset(2026, 10, 9, 23, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two"));
        string version = SaveConfig();
        AddAd(strongAd with { Deadline = "2026-10-09" }, juniorLabel, Fit.Strong, version);
        StartRun();

        await RunAsync();

        Assert.Equal(noMatches, console.Output);
    }

    [Theory]
    [InlineData(Locales.English, "No config yet. Set up with: jobranger init\n")]
    [InlineData(Locales.Norwegian, "Ingen oppsettsfil ennå. Sett opp med: jobranger init\n")]
    public async Task ShouldPrintConfigMissing_WhenRunHasNoConfig(string lang, string line) {
        environment["LANG"] = lang;
        StartRun();

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, line, "report.failed config_missing"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Theory]
    [InlineData(Locales.English, "No profile file at {0}. Set up with: jobranger init\n")]
    [InlineData(Locales.Norwegian, "Ingen profilfil på {0}. Sett opp med: jobranger init\n")]
    public async Task ShouldNameProfileFile_WhenProfileIsMissing(string lang, string line) {
        environment["LANG"] = lang;
        string profile = temp.File("moved.yaml");
        ConfigFile.Save(paths.ConfigPath(), RunFixtures.Config(profile), temp.Path, NullLogger.Instance);
        StartRun();

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(line, profile), "report.failed profile_missing"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintConfigErrors_WhenConfigIsInvalid() {
        StartRun();
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ConfigPath(), "unknown_key: 1\n");

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, "report.failed config_invalid"), (exit, CommandFixtures.LoggedFailure(paths)));
        Assert.Contains("unknown_key: is not a known setting\n", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer() {
        CommandFixtures.MarkSchemaTooNew(paths.DbPath());

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, "The database was made by a newer jobranger. Update jobranger and try again.\n", "report.failed schema_too_new"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt() {
        Directory.CreateDirectory(paths.DataDir());
        File.WriteAllBytes(paths.DbPath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, paths.DbPath()), "report.failed database_unreadable"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenMigrationFails() {
        CommandFixtures.RewindSchema(paths.DbPath());

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, paths.DbPath()), "report.failed migration_failed"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Theory(Timeout = testTimeoutMs)]
    [InlineData("abc", "report.failed", "report")]
    [InlineData("99999999999999", "report.failed", "report")]
    [InlineData("-1", "report.failed", "report")]
    [InlineData("abc", "devices.failed", "devices", "list")]
    [InlineData("abc", "pairing.failed", "pair")]
    public async Task ShouldPrintDatabaseUnreadable_WhenSchemaVersionIsNotNumber(
        string version, string failedEvent, params string[] args) {
        using (SqliteConnection database = Database.Open(paths.DbPath(), NullLogger.Instance)) {
            new StateStore(database).Set(StateStore.SchemaVersionKey, version);
        }

        int exit = await RunCommandAsync(args, TestContext.Current.CancellationToken);

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, paths.DbPath()), $"{failedEvent} database_unreadable"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsSymlink() {
        Directory.CreateDirectory(paths.DataDir());
        File.CreateSymbolicLink(paths.DbPath(), temp.File("elsewhere.db"));

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, paths.DbPath()), "report.failed database_unreadable"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDirNotWritable_WhenDataParentIsReadOnly() {
        string parent = temp.File("locked");
        Directory.CreateDirectory(parent);
        File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        string data = Path.Combine(parent, "data");
        environment[dataDirVariable] = data;

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DirNotWritable, data), "report.failed dir_not_writable"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDirNotWritable_WhenDataParentIsFile() {
        string parent = temp.File("taken");
        File.WriteAllText(parent, string.Empty);
        string data = Path.Combine(parent, "data");
        environment[dataDirVariable] = data;

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DirNotWritable, data), "report.failed dir_not_writable"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task ShouldExitWithoutTrace_WhenOutputCannotBeWritten(Type failure) {
        int exit = await RunAsync(CommandFixtures.BrokenConsole(failure));

        Assert.Equal(
            (CliExitCodes.Failure, string.Empty, "report.failed output_failed"),
            (exit, console.Errors, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldExitWithoutTrace_WhenFailureLineCannotBeWritten() {
        StartRun();

        int exit = await RunAsync(CommandFixtures.BrokenConsole(typeof(IOException)));

        Assert.Equal(
            (CliExitCodes.Failure, string.Empty, "report.failed output_failed"),
            (exit, console.Errors, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public void ShouldOfferReportCommand_WhenRootIsBuilt() {
        RootCommand root = new(CliServices.Create(clock, console.Console, console.ErrorConsole, environment));

        Assert.Contains(ReportCommand.CommandName, root.Subcommands.Select(command => command.Name));
    }

    private string SaveConfig() {
        AppConfig config = RunFixtures.Config(MatchingFixtures.TestData("profile_user.yaml"));
        ConfigFile.Save(paths.ConfigPath(), config, temp.Path, NullLogger.Instance);
        return PromptSetup.Load(config, paths).Version;
    }

    private void AddAd(Ad ad, string label, string status, string version, TimeProvider? seenAt = null, double? years = null) {
        using SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance);
        new AdStore(connection, seenAt ?? clock).Upsert(ad);
        Match match = MatchingFixtures.StrongMatch with { Status = status, RequiredExperienceYears = years, Version = version };
        StoredClassification classification = AdFixtures.Classification with {
            Label = label,
            DisplayTitle = ad.DisplayTitle,
            Assessment = MatchingEntry.EncodeMatch(match, []),
        };
        new AdsEntry(connection, clock).SetClassification(ad.Id, classification, version);
    }

    private void WithStore(Action<AdStore> change) {
        using SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance);
        change(new AdStore(connection, clock));
    }

    private void StartRun() {
        using SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance);
        new RunStore(connection).Start(AdFixtures.Now);
    }

    private Task<int> RunAsync(IAnsiConsole? output = null) {
        CliServices services = CliServices.Create(clock, output ?? console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new ReportCommand(services, environment, clock)];
        InvocationConfiguration configuration = new() { Error = console.ErrorConsole.Profile.Out.Writer };
        return root.Parse([ReportCommand.CommandName]).InvokeAsync(configuration, TestContext.Current.CancellationToken);
    }

    private Task<int> RunCommandAsync(string[] args, CancellationToken cancellationToken) {
        CliServices services = CliServices.Create(clock, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [
            new ReportCommand(services, environment, clock),
            new DevicesCommand(services, environment, clock, LinkEntry.OpenAsync),
            new PairCommand(services, environment, clock, inputRedirected: true, LinkEntry.OpenAsync),
        ];
        return root.Parse(args).InvokeAsync(null, cancellationToken);
    }
}
