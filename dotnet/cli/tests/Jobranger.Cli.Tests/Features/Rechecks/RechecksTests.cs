using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Features.Rechecks;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using ModelsEntry = Jobranger.Cli.Features.Models.Models;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Tests.Features.Rechecks;

[UnsupportedOSPlatform("windows")]
public sealed class RechecksTests : IDisposable {
    private const string guidance = "Prefer backend roles.";
    private const string noConfig = "no config";
    private const string invalidConfig = "invalid config";
    private const string schemaTooNew = "schema too new";
    private const string corruptDatabase = "corrupt database";
    private const string databaseLink = "database link";
    private const string dataParentFile = "data parent file";
    private const string migrationClash = "migration clash";
    private const string dataDirVariable = "JOBRANGER_DATA_DIR";
    private const string noModelName = "model:\n  provider: anthropic\n";
    private const string tooNewSchema = "99";
    private const string fileTightenedEvent = "paths.private_file.tightened";
    private const int staleAds = 3;
    private const int corruptBytes = 4096;

    private readonly TempDir temp = new();
    private readonly Hashtable environment;
    private readonly Paths paths;
    private readonly RechecksEntry rechecks;

    public RechecksTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
        rechecks = new RechecksEntry(paths, AdFixtures.Clock(), NullLogger.Instance);
    }

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldReturnRecheckEstimate_WhenGuidanceIsSaved() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.PricedModel);
        EditFixtures.AddAds(paths, staleAds, AdFixtures.OutdatedVersion);

        SaveResult result = rechecks.SaveGuidance(guidance);

        Assert.Equal(BudgetEntry.EstimateRecheck(staleAds, Prompt().System.Length, ConfiguredPrices()), result.Estimate);
        Assert.NotNull(result.Estimate?.CostUsd);
    }

    [Fact]
    public void ShouldCountCurrentAds_WhenGuidanceChangesPrompt() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.PricedModel);
        EditFixtures.AddAds(paths, 1, Prompt().Version);

        SaveResult result = rechecks.SaveGuidance(guidance);

        Assert.Equal(1, result.Estimate?.Ads);
    }

    [Theory]
    [InlineData(EditFixtures.NoModel)]
    [InlineData(noModelName)]
    [InlineData(EditFixtures.UnpricedModel)]
    public void ShouldLeaveOutCost_WhenModelHasNoPrice(string model) {
        EditFixtures.WriteSetup(temp, paths, model);
        EditFixtures.AddAds(paths, 1, AdFixtures.OutdatedVersion);

        RecheckEstimate? estimate = rechecks.Estimate();

        Assert.Equal(new RecheckEstimate(1, null), estimate);
    }

    [Theory]
    [InlineData(noConfig)]
    [InlineData(invalidConfig)]
    [InlineData(schemaTooNew)]
    [InlineData(corruptDatabase)]
    [InlineData(databaseLink)]
    [InlineData(dataParentFile)]
    [InlineData(migrationClash)]
    public void ShouldReturnNoEstimate_WhenInputsCannotBeRead(string failure) {
        Prepare(failure);

        RecheckEstimate? estimate = rechecks.Estimate();

        Assert.Null(estimate);
    }

    [Fact]
    public void ShouldReturnNoEstimate_WhenAccessIsDenied() {
        EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
        Directory.CreateDirectory(paths.DataDir());
        File.WriteAllText(paths.DbPath(), string.Empty);
        File.SetUnixFileMode(paths.DbPath(), FileModes.OwnerReadWrite | UnixFileMode.GroupRead);
        RechecksEntry denied = new(paths, AdFixtures.Clock(), new DeniedLogger());

        RecheckEstimate? estimate = denied.Estimate();

        Assert.Null(estimate);
    }

    [Fact]
    public void ShouldKeepDocuments_WhenAboutIsSaved() {
        ProfilesEntry.SaveContext(
            paths.ContextPath(), new UserContext { Docs = [ContextFixtures.Doc] }, NullLogger.Instance);

        rechecks.SaveAbout(ContextFixtures.About);

        UserContext saved = ProfilesEntry.LoadContext(paths.ContextPath());
        Assert.Equal(ContextFixtures.About, saved.About);
        Assert.Equal(ContextFixtures.Doc, Assert.Single(saved.Docs));
    }

    [Fact]
    public void ShouldStripText_WhenAboutIsSaved() {
        rechecks.SaveAbout("  text\n");

        Assert.Equal("text", ProfilesEntry.LoadContext(paths.ContextPath()).About);
    }

    private void Prepare(string failure) {
        switch (failure) {
            case noConfig:
                EditFixtures.WriteProfile(temp);
                return;
            case invalidConfig:
                EditFixtures.WriteConfig(paths, EditFixtures.UnknownSetting);
                return;
            default:
                EditFixtures.WriteSetup(temp, paths, EditFixtures.NoModel);
                BreakDatabase(failure);
                return;
        }
    }

    private void BreakDatabase(string failure) {
        switch (failure) {
            case schemaTooNew:
                using (SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance)) {
                    new StateStore(connection).Set(StateStore.SchemaVersionKey, tooNewSchema);
                }
                return;
            case corruptDatabase:
                Directory.CreateDirectory(paths.DataDir());
                File.WriteAllText(paths.DbPath(), new string('x', corruptBytes));
                return;
            case databaseLink:
                Directory.CreateDirectory(paths.DataDir());
                File.CreateSymbolicLink(paths.DbPath(), temp.File("elsewhere.db"));
                return;
            case dataParentFile:
                File.WriteAllText(temp.File("taken"), string.Empty);
                environment[dataDirVariable] = Path.Combine(temp.File("taken"), "data");
                return;
            default:
                Directory.CreateDirectory(paths.DataDir());
                using (SqliteConnection connection = TestDatabase.OpenRaw(paths.DbPath())) {
                    TestDatabase.Execute(connection, "CREATE TABLE ads (id TEXT)");
                }
                return;
        }
    }

    private PromptSetup Prompt() => PromptSetup.Load(ConfigFile.Load(paths.ConfigPath(), paths.Home()), paths);

    private ModelPrices ConfiguredPrices() {
        ModelConfig model = ConfigFile.Load(paths.ConfigPath(), paths.Home()).Model;
        return RunsEntry.ToPrices(ModelsEntry.SpecFor(model.Provider!.Value, model.Model!, model.Price)!);
    }

    private sealed class DeniedLogger : ILogger {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (eventId.Name == fileTightenedEvent) {
                throw new UnauthorizedAccessException();
            }
        }
    }
}
