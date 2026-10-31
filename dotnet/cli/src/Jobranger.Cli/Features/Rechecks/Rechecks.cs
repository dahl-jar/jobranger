using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Models;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ModelsEntry = Jobranger.Cli.Features.Models.Models;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Features.Rechecks;

[UnsupportedOSPlatform("windows")]
internal sealed class Rechecks(Paths paths, TimeProvider time, ILogger logger) {
    public SaveResult SaveConfig(string text) {
        ConfigFile.SaveText(paths.ConfigPath(), text, paths.Home(), logger);
        return new SaveResult(Empty: false, Estimate());
    }

    public SaveResult SaveGuidance(string text) {
        GuidanceText saved = MatchingEntry.SaveGuidance(paths.GuidancePath(), text, logger);
        return new SaveResult(saved.Text.Length == 0, Estimate());
    }

    public SaveResult ResetGuidance() {
        MatchingEntry.ResetGuidance(paths.GuidancePath(), logger);
        return new SaveResult(Empty: true, Estimate());
    }

    public SaveResult SaveAbout(string text) {
        string about = PythonUnicode.Strip(text);
        UserContext context = ProfilesEntry.LoadContext(paths.ContextPath()) with { About = about };
        ProfilesEntry.SaveContext(paths.ContextPath(), context, logger);
        return new SaveResult(about.Length == 0, Estimate());
    }

    public RecheckEstimate? Estimate() {
        try {
            AppConfig config = ConfigFile.Load(paths.ConfigPath(), paths.Home());
            PromptSetup prompt = PromptSetup.Load(config, paths);
            using SqliteConnection connection = Database.Open(paths.DbPath(), logger);
            long outdated = new AdsEntry(connection, time).CountOutdated(prompt.Version);
            return BudgetEntry.EstimateRecheck(
                (int)Math.Min(outdated, int.MaxValue), prompt.System.Length, PricesOf(config.Model));
        } catch (Exception error) when (error is ConfigMissingException or ConfigInvalidException
            or SchemaTooNewException or MigrationException or SqliteException or PrivateFileException
            or IOException or UnauthorizedAccessException) {
            LogEvents.RecheckSkipped(logger, error.GetType().Name);
            return null;
        }
    }

    private static ModelPrices? PricesOf(ModelConfig model) {
        if (model.Provider is not ProviderName provider || model.Model is not string modelId) {
            return null;
        }
        return ModelsEntry.SpecFor(provider, modelId, model.Price) is ModelSpec spec ? RunsEntry.ToPrices(spec) : null;
    }
}
