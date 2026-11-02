using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Models;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ModelsEntry = Jobranger.Cli.Features.Models.Models;

namespace Jobranger.Cli.Features.Runs;

[UnsupportedOSPlatform("windows")]
internal sealed class Runs(
    SqliteConnection connection,
    Paths paths,
    HttpClient http,
    Func<ModelConfig, bool, CancellationToken, Task<IModelClient>> createModel,
    Func<SourcesConfig, IReadOnlyList<ISource>> enabledSources,
    TimeProvider time,
    ILogger logger) {
    public static AdView ToAdView(Ad ad) => RunConversions.ToAdView(ad);

    public static DigestCard ToDigestCard(AdView view, string url, Targets targets) =>
        RunConversions.ToDigestCard(view, url, targets);

    public static IReadOnlyList<HiddenCount> ToHiddenCounts(IReadOnlyDictionary<HiddenKind, int> counts) =>
        RunConversions.ToHiddenCounts(counts);

    public static IReadOnlyList<RunRow> Recent(SqliteConnection connection, int limit) => new RunStore(connection).List(limit);

    public static RunRow? LastRun(SqliteConnection connection) => new RunStore(connection).Last();

    public static IReadOnlyList<string> Report(
        SqliteConnection connection, AppConfig config, Paths paths, RunRow lastRun, TimeProvider time, IMessages messages) =>
        TerminalReport.Lines(connection, config, paths, lastRun, time, messages);

    public async Task<RunResult> RunAsync(RunRequest request, CancellationToken cancellationToken) {
        RunModel? model = await CreateModelAsync(request.Config.Model, cancellationToken).ConfigureAwait(false);
        try {
            using FileLock? held = FileLock.TryAcquire(paths.RunLockPath(), logger);
            if (held is null) {
                LogEvents.RunLocked(logger);
                return RunResult.Locked();
            }
            RunPass pass = new(
                new AdsEntry(connection, time),
                new BudgetEntry(connection),
                new RunStore(connection),
                paths,
                http,
                enabledSources,
                time,
                logger);
            return await pass.RunAsync(request, model, cancellationToken).ConfigureAwait(false);
        } finally {
            model?.Client.Dispose();
        }
    }

    private async Task<RunModel?> CreateModelAsync(ModelConfig model, CancellationToken cancellationToken) {
        if (model.Provider is not ProviderName provider || model.Model is not string modelId) {
            return null;
        }
        ModelSpec? spec = ModelsEntry.SpecFor(provider, modelId, model.Price);
        IModelClient client;
        try {
            client = await createModel(model, spec?.SupportsEffort ?? false, cancellationToken).ConfigureAwait(false);
        } catch (ModelSetupException) {
            return null;
        }
        return new RunModel(client, MatchingEntry.SchemaFor(provider), spec is null ? null : RunConversions.ToPrices(spec));
    }
}
