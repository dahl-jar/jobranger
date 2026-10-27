using System.Globalization;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Features.Runs;

internal sealed class RunPass(
    AdsEntry ads,
    BudgetEntry budget,
    RunStore runs,
    Paths paths,
    HttpClient http,
    Func<SourcesConfig, IReadOnlyList<ISource>> enabledSources,
    TimeProvider time,
    ILogger logger) {
    private const int applyUrlBackfillLimit = 60;

    private static readonly FetchTally noFetch = new(0, 0, [], [], false);
    private static readonly IReadOnlyDictionary<string, string> noJobTitles = new Dictionary<string, string>();

    public async Task<RunResult> RunAsync(RunRequest request, RunModel? model, CancellationToken cancellationToken) {
        DateTimeOffset started = time.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(started, time.LocalTimeZone).DateTime);
        long runId = runs.Start(started);
        using IDisposable scope = RunScope.Bind(runId.ToString(CultureInfo.InvariantCulture));
        Progress progress = new();
        RunStatus status = RunStatus.Failed;
        try {
            PromptSetup prompt = Setup(request.Config);
            progress.Version = prompt.Version;
            try {
                status = await StagesAsync(request, model, prompt, started, today, progress, cancellationToken)
                    .ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                progress.Report = ReportOutcome.Empty;
                status = RunStatus.Cancelled;
            }
        } finally {
            runs.Finish(runId, new RunSummary(
                time.GetUtcNow(),
                status,
                progress.Tally.AdsRead,
                progress.Tally.NewAds,
                progress.Report.ReportedIds.Count,
                progress.Report.HiddenCounts.Values.Sum(),
                progress.Tally.Failed));
        }
        return new RunResult {
            RunId = runId,
            Status = status,
            Mode = request.Mode,
            AdsRead = progress.Tally.AdsRead,
            NewAds = progress.Tally.NewAds,
            FailedSources = progress.Tally.Failed,
            SourcesRead = progress.Tally.SourcesRead,
            ReportedIds = progress.Report.ReportedIds,
            CarriedIds = progress.Report.CarriedIds,
            SilentIds = progress.Report.SilentIds,
            HiddenCounts = progress.Report.HiddenCounts,
            Pending = progress.Classified.Pending,
            ModelStopped = progress.Classified.Stopped,
            Version = progress.Version,
        };
    }

    private PromptSetup Setup(AppConfig config) {
        Profile profile = ProfilesEntry.Load(config.Profile.Path);
        UserContext context = ProfilesEntry.LoadContext(paths.ContextPath());
        IReadOnlySet<string> evidence = ProfilesEntry.CandidateEvidence(profile, context, paths.ContextPath());
        GuidanceText guidance = MatchingEntry.LoadGuidance(paths.GuidancePath());
        string system = MatchingEntry.BuildSystem(
            guidance.Text,
            MatchingEntry.LimitRules(config.Targets.Limits),
            ProfilesEntry.Text(profile),
            ProfilesEntry.ContextText(context));
        return new PromptSetup(
            system, MatchingEntry.AssessmentVersion(system), evidence, ProfilesEntry.EducationIds(profile));
    }

    private async Task<RunStatus> StagesAsync(
        RunRequest request,
        RunModel? model,
        PromptSetup prompt,
        DateTimeOffset started,
        DateOnly today,
        Progress progress,
        CancellationToken cancellationToken) {
        AppConfig config = request.Config;
        IReadOnlyDictionary<string, string> jobTitles = noJobTitles;
        if (!cancellationToken.IsCancellationRequested) {
            (progress.Tally, jobTitles) = await FetchAsync(request, prompt.Version, started, cancellationToken)
                .ConfigureAwait(false);
        }
        budget.RecordRead(today, progress.Tally.AdsRead);
        if (progress.Tally.Cancelled || cancellationToken.IsCancellationRequested) {
            return RunStatus.Cancelled;
        }
        progress.Classified = await new ClassifyStage(ads, budget, request.Events, logger)
            .RunAsync(new ClassifyInput(model, prompt, config.Targets.Limits, today), cancellationToken)
            .ConfigureAwait(false);
        if (progress.Classified.Stopped == StopReason.Cancelled) {
            return RunStatus.Cancelled;
        }
        IReadOnlyList<EmployerFeed> configured = config.Sources.Employers ? config.Sources.EmployerFeeds : [];
        List<EmployerFeed> readFeeds = [.. configured.Where(feed => progress.Tally.SourcesRead.Contains(feed.Source))];
        progress.Report = await new ReportStage(ads, http, time, request.Events, logger)
            .RunAsync(new ReportInput(config.Targets, prompt.Version, today, started, readFeeds, jobTitles), cancellationToken)
            .ConfigureAwait(false);
        return RunStatus.Completed;
    }

    private async Task<(FetchTally Tally, IReadOnlyDictionary<string, string> JobTitles)> FetchAsync(
        RunRequest request, string version, DateTimeOffset started, CancellationToken cancellationToken) {
        AppConfig config = request.Config;
        IReadOnlyList<ISource> sources = enabledSources(config.Sources);
        if (sources.Count == 0) {
            return (noFetch, noJobTitles);
        }
        SourcePlan plan = CatalogueEntry.Load().PlanFor(config.Targets);
        IEnumerable<Ad> pending = [
            .. ads.PendingDescription(plan.SkipTitleLevels),
            .. ads.PendingApplyUrl(SourcesEntry.Finn, applyUrlBackfillLimit, config.Targets),
        ];
        FetchContext context = new() {
            Http = http,
            Plan = plan,
            RunStarted = started,
            KnownIds = ads.KnownIds(),
            Cursors = ads.Cursors(),
            Logger = logger,
            PendingPages = [.. pending.Select(RunConversions.ToListing)],
        };
        AdsBatchWriter writer = new(ads, version, started);
        FetchTally tally = await new FetchStage(time, request.Events)
            .RunAsync(sources, context, writer, cancellationToken)
            .ConfigureAwait(false);
        return (tally, writer.JobTitles);
    }

    private sealed class Progress {
        public FetchTally Tally { get; set; } = noFetch;

        public ClassifyOutcome Classified { get; set; } = new(0, null);

        public ReportOutcome Report { get; set; } = ReportOutcome.Empty;

        public string? Version { get; set; }
    }
}
