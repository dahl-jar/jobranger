using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Features.Runs;

internal sealed class ClassifyStage(
    AdsEntry ads, BudgetEntry budget, TimeProvider time, IRunEvents events, ILogger logger) {
    private const int labelMaxAttempts = 3;

    public async Task<ClassifyOutcome> RunAsync(ClassifyInput input, CancellationToken cancellationToken) {
        string version = input.Prompt.Version;
        ads.ResetUnassessed(version);
        IReadOnlyList<Ad> outdated = ads.Outdated(version);
        if (outdated.Count > 0) {
            LogEvents.OutdatedQueued(logger, outdated.Count);
        }
        (IReadOnlyList<AdView> representatives, Dictionary<string, IReadOnlyList<Ad>> groups) =
            ReuseOrPickRepresentatives(version, outdated);
        ClassifyOutcome outcome = input.Model is RunModel model
            ? await ClassifyAsync(input, model, representatives, groups, cancellationToken).ConfigureAwait(false)
            : new ClassifyOutcome(representatives.Count, StopReason.Auth);
        foreach (Ad ad in ads.Exhausted(labelMaxAttempts, version)) {
            ads.SetLabel(ad.Id, AdsEntry.ErrorLabel, version);
        }
        return outcome;
    }

    private static bool CanReuse(Ad ad, string version) =>
        ad.Label is not null and not AdsEntry.ErrorLabel
        && ad.IsProgrammingJob is not null
        && MatchingEntry.HasCurrentMatch(RunConversions.ToAdView(ad), version);

    private static bool IsRetryable(ApiErrorKind kind) =>
        kind is ApiErrorKind.RateLimit or ApiErrorKind.Overloaded or ApiErrorKind.Server or ApiErrorKind.Connection;

    private static async Task<ModelReply> CallAsync(
        RunModel model, PromptText text, CancellationToken cancellationToken) {
        return await model.Client.ClassifyAsync(new ClassifyPrompt(text.System, text.User), model.Schema, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<StopReason?> PauseAsync(
        ModelCalls calls, CancellationToken stageToken, CancellationToken runToken) {
        try {
            await calls.PauseAsync(stageToken).ConfigureAwait(false);
            return null;
        } catch (OperationCanceledException) when (runToken.IsCancellationRequested) {
            return StopReason.Cancelled;
        }
    }

    private async Task<ClassifyOutcome> ClassifyAsync(
        ClassifyInput input,
        RunModel model,
        IReadOnlyList<AdView> representatives,
        Dictionary<string, IReadOnlyList<Ad>> groups,
        CancellationToken cancellationToken) {
        ModelCalls calls = new(representatives, new SharedBackoff(time));
        using CancellationTokenSource stage = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Progress progress = new(representatives.Count);
        try {
            while (true) {
                progress.Stopped ??= StartCalls(calls, input, model, groups, stage.Token, cancellationToken);
                if (calls.Running > 0) {
                    await ReceiveAsync(calls, input, model, groups, progress, cancellationToken).ConfigureAwait(false);
                } else if (progress.Stopped is not null || !calls.HasWaiting) {
                    break;
                } else {
                    progress.Stopped = await PauseAsync(calls, stage.Token, cancellationToken).ConfigureAwait(false);
                }
            }
        } catch {
            await stage.CancelAsync().ConfigureAwait(false);
            await calls.WhenAllDone().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            RecordFinished(calls, input.Today, model);
            throw;
        }
        return progress.Outcome();
    }

    private StopReason? StartCalls(
        ModelCalls calls,
        ClassifyInput input,
        RunModel model,
        Dictionary<string, IReadOnlyList<Ad>> groups,
        CancellationToken stageToken,
        CancellationToken runToken) {
        while (calls.CanStart) {
            if (runToken.IsCancellationRequested) {
                return StopReason.Cancelled;
            }
            PromptText text = MatchingEntry.BuildPrompt(calls.Next, input.Prompt.System, input.Today, []);
            decimal callUsd = model.Prices is ModelPrices prices
                ? budget.EstimateCallCost(
                    prices, text.System.Length + text.User.Length, RequestBodies.MaxOutputTokens, input.Today)
                : 0m;
            if (budget.ExceedsLimit(input.Config, model.Prices, input.Today, callUsd)) {
                DropOverLimit(calls, groups);
            } else if (budget.Allowed(input.Config, model.Prices, input.Today, calls.ReservedUsd, callUsd)) {
                calls.Start(CallAsync(model, text, stageToken), callUsd);
            } else if (calls.Running > 0) {
                return null;
            } else {
                calls.SetAside();
            }
        }
        return calls.Running == 0 && !calls.HasWaiting && calls.HasSetAside ? PauseBudget() : null;
    }

    private void DropOverLimit(ModelCalls calls, Dictionary<string, IReadOnlyList<Ad>> groups) {
        string adId = calls.Next.Id;
        BumpAttempts(groups[adId]);
        calls.Drop();
        LogEvents.ClassifyAdOverLimit(logger, adId);
    }

    private StopReason PauseBudget() {
        LogEvents.ClassifyBudgetPaused(logger);
        return StopReason.Budget;
    }

    private async Task ReceiveAsync(
        ModelCalls calls,
        ClassifyInput input,
        RunModel model,
        Dictionary<string, IReadOnlyList<Ad>> groups,
        Progress progress,
        CancellationToken cancellationToken) {
        (AdView representative, Task<ModelReply> call) = await calls.NextFinishedAsync().ConfigureAwait(false);
        ModelReply reply;
        try {
            reply = await call.ConfigureAwait(false);
        } catch (ApiException error) {
            if (error.Kind == ApiErrorKind.BadRequest) {
                BumpAttempts(groups[representative.Id]);
            }
            progress.Stopped ??= Fail(calls, representative, error);
            return;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            progress.Stopped ??= StopReason.Cancelled;
            return;
        }
        calls.Answered();
        progress.Done++;
        Record(input.Today, model, reply);
        bool citedUnknownEvidence = StoreReply(input, representative, reply, groups[representative.Id]);
        if (citedUnknownEvidence) {
            progress.Dropped++;
        }
        events.Emit(new RunEvent(RunStage.Model, Checked: progress.Done, Total: progress.Total));
    }

    private StopReason? Fail(ModelCalls calls, AdView representative, ApiException error) {
        string failure = WireNames.Of(error.Kind);
        if (IsRetryable(error.Kind) && calls.TryRequeue(representative, error)) {
            LogEvents.ClassifyCallRetried(logger, failure);
            return null;
        }
        LogEvents.ClassifyProviderFailed(logger, failure);
        return error.Kind == ApiErrorKind.Auth ? StopReason.Auth : StopReason.ProviderDown;
    }

    private void Record(DateOnly today, RunModel model, ModelReply reply) {
        TokenCounts tokens = RunConversions.ToTokens(reply.Usage);
        decimal cost = reply.ReportedCostUsd
            ?? (model.Prices is ModelPrices prices ? BudgetEntry.Cost(prices, tokens, batch: false) : 0m);
        budget.Record(new SpendRecord(today, tokens, cost));
    }

    private void RecordFinished(ModelCalls calls, DateOnly today, RunModel model) {
        foreach (ModelReply reply in calls.Finished()) {
            try {
                Record(today, model, reply);
            } catch (SqliteException error) {
                LogEvents.ClassifySpendFailed(logger, error);
            }
        }
    }

    private bool StoreReply(ClassifyInput input, AdView representative, ModelReply reply, IReadOnlyList<Ad> group) {
        AdAssessment? assessment = reply.Stop == StopKind.Done ? MatchingEntry.ParseAssessment(reply.Text) : null;
        Classification? classification = assessment is null
            ? null
            : MatchingEntry.ToClassification(
                assessment,
                representative,
                input.Prompt.Evidence,
                input.Prompt.Education,
                input.Config.Targets.Limits,
                input.Prompt.Version);
        if (classification is not null) {
            StoreGroup(group, classification, input.Prompt.Version);
            return false;
        }
        BumpAttempts(group);
        return assessment is not null
            && MatchingEntry.CitesUnknownEvidence(assessment, input.Prompt.Evidence, input.Prompt.Version);
    }

    private void BumpAttempts(IReadOnlyList<Ad> group) => ads.BumpLabelAttempts([.. group.Select(ad => ad.Id)]);

    private (IReadOnlyList<AdView> Representatives, Dictionary<string, IReadOnlyList<Ad>> Groups) ReuseOrPickRepresentatives(
        string version, IReadOnlyList<Ad> outdated) {
        HashSet<string> targetIds = [.. ads.Pending(labelMaxAttempts).Concat(outdated).Select(ad => ad.Id)];
        List<AdView> fresh = [];
        List<AdView> reassessed = [];
        Dictionary<string, IReadOnlyList<Ad>> groups = new(StringComparer.Ordinal);
        foreach (IReadOnlyList<Ad> group in AdsEntry.GroupDuplicates(ads.Active())) {
            List<Ad> targets = [.. group.Where(ad => targetIds.Contains(ad.Id))];
            if (targets.Count == 0) {
                continue;
            }
            List<AdView> completed = [.. group.Where(ad => CanReuse(ad, version)).Select(RunConversions.ToAdView)];
            if (completed.Count > 0) {
                AdView preferred = MatchingEntry.PreferredAd(completed, version);
                StoreGroup(targets, RunConversions.ToClassification(preferred), version);
                continue;
            }
            AdView representative = MatchingEntry.PreferredAd([.. targets.Select(RunConversions.ToAdView)], version);
            (targets.Any(ad => ad.Label is null) ? fresh : reassessed).Add(representative);
            groups[representative.Id] = targets;
        }
        return ([.. fresh, .. reassessed], groups);
    }

    private void StoreGroup(IReadOnlyList<Ad> group, Classification classification, string version) {
        foreach (Ad ad in group) {
            ads.SetClassification(
                ad.Id, RunConversions.ToStored(MatchingEntry.WithTitleLevel(ad.Title, classification)), version);
        }
    }

    private sealed class Progress(int total) {
        public int Total { get; } = total;

        public int Done { get; set; }

        public int Dropped { get; set; }

        public StopReason? Stopped { get; set; }

        public ClassifyOutcome Outcome() => new(Total - Done, Stopped, Dropped);
    }
}
