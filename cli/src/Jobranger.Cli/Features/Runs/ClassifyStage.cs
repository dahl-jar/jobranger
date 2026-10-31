using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Features.Runs;

internal sealed class ClassifyStage(AdsEntry ads, BudgetEntry budget, IRunEvents events, ILogger logger) {
    public const string ErrorLabel = "error";

    private const int labelMaxAttempts = 3;

    public async Task<ClassifyOutcome> RunAsync(ClassifyInput input, CancellationToken cancellationToken) {
        string version = input.Prompt.Version;
        if (ads.RefreshAssessments(version)) {
            LogEvents.AssessmentsRefreshed(logger);
        }
        (IReadOnlyList<AdView> representatives, Dictionary<string, IReadOnlyList<Ad>> groups) =
            ReuseOrPickRepresentatives(version);
        ClassifyOutcome outcome = input.Model is RunModel model
            ? await ClassifyAsync(input, model, representatives, groups, cancellationToken).ConfigureAwait(false)
            : new ClassifyOutcome(representatives.Count, StopReason.Auth);
        foreach (Ad ad in ads.Exhausted(labelMaxAttempts)) {
            ads.SetLabel(ad.Id, ErrorLabel, version);
        }
        return outcome;
    }

    private static bool CanReuse(Ad ad, string version) =>
        ad.Label is not null and not ErrorLabel
        && ad.IsProgrammingJob is not null
        && MatchingEntry.HasCurrentMatch(RunConversions.ToAdView(ad), version);

    private async Task<ClassifyOutcome> ClassifyAsync(
        ClassifyInput input,
        RunModel model,
        IReadOnlyList<AdView> representatives,
        Dictionary<string, IReadOnlyList<Ad>> groups,
        CancellationToken cancellationToken) {
        int done = 0;
        StopReason? stopped = null;
        foreach (AdView representative in representatives) {
            if (cancellationToken.IsCancellationRequested) {
                stopped = StopReason.Cancelled;
                break;
            }
            PromptText text = MatchingEntry.BuildPrompt(representative, input.Prompt.System, input.Today, []);
            ModelReply reply;
            try {
                reply = await model.Client.ClassifyAsync(new ClassifyPrompt(text.System, text.User), model.Schema, cancellationToken)
                    .ConfigureAwait(false);
            } catch (ApiException error) {
                LogEvents.ClassifyProviderFailed(logger, WireNames.Of(error.Kind));
                stopped = error.Kind == ApiErrorKind.Auth ? StopReason.Auth : StopReason.ProviderDown;
                break;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                stopped = StopReason.Cancelled;
                break;
            }
            done++;
            Record(input.Today, model, reply);
            StoreReply(input, representative, reply, groups[representative.Id]);
            events.Emit(new RunEvent(RunStage.Model, Checked: done, Total: representatives.Count));
        }
        return new ClassifyOutcome(stopped is null ? 0 : representatives.Count - done, stopped);
    }

    private void Record(DateOnly today, RunModel model, ModelReply reply) {
        TokenCounts tokens = RunConversions.ToTokens(reply.Usage);
        decimal cost = reply.ReportedCostUsd
            ?? (model.Prices is ModelPrices prices ? BudgetEntry.Cost(prices, tokens, batch: false) : 0m);
        budget.Record(new SpendRecord(today, tokens, cost));
    }

    private void StoreReply(ClassifyInput input, AdView representative, ModelReply reply, IReadOnlyList<Ad> group) {
        Classification? classification = reply.Stop == StopKind.Done
            && MatchingEntry.ParseAssessment(reply.Text) is AdAssessment assessment
            ? MatchingEntry.ToClassification(
                assessment, representative, input.Prompt.Evidence, input.Prompt.Education, input.Limits, input.Prompt.Version)
            : null;
        if (classification is null) {
            ads.BumpLabelAttempts([.. group.Select(ad => ad.Id)]);
        } else {
            StoreGroup(group, classification, input.Prompt.Version);
        }
    }

    private (IReadOnlyList<AdView> Representatives, Dictionary<string, IReadOnlyList<Ad>> Groups) ReuseOrPickRepresentatives(
        string version) {
        HashSet<string> pending = [.. ads.Pending(labelMaxAttempts).Select(ad => ad.Id)];
        List<AdView> representatives = [];
        Dictionary<string, IReadOnlyList<Ad>> groups = new(StringComparer.Ordinal);
        foreach (IReadOnlyList<Ad> group in AdsEntry.GroupDuplicates(ads.Active())) {
            List<Ad> targets = [.. group.Where(ad => pending.Contains(ad.Id))];
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
            representatives.Add(representative);
            groups[representative.Id] = targets;
        }
        return (representatives, groups);
    }

    private void StoreGroup(IReadOnlyList<Ad> group, Classification classification, string version) {
        foreach (Ad ad in group) {
            ads.SetClassification(
                ad.Id, RunConversions.ToStored(MatchingEntry.WithTitleLevel(ad.Title, classification)), version);
        }
    }
}
