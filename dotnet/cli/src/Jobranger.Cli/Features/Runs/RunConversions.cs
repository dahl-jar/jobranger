using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Models;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Storage;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Features.Runs;

internal static class RunConversions {
    private static readonly string expiredStatus = WireNames.Of(ApplyStatus.Expired);

    public static Ad ToAd(SourceListing listing) => new() {
        Id = listing.Id,
        Source = listing.Source,
        SourceId = listing.SourceId,
        Title = listing.Title,
        Company = listing.Company,
        Location = listing.Location,
        Published = listing.Published,
        Deadline = listing.Deadline,
        Url = listing.Url,
        Description = listing.Description,
        ApplyUrl = listing.ApplyUrl,
        Republished = listing.Republished,
    };

    public static SourceListing ToListing(Ad ad) => new() {
        Id = ad.Id,
        Source = ad.Source,
        SourceId = ad.SourceId,
        Title = ad.Title,
        Company = ad.Company,
        Location = ad.Location,
        Published = ad.Published,
        Deadline = ad.Deadline,
        Url = ad.Url,
        Description = ad.Description,
        ApplyUrl = ad.ApplyUrl,
        Republished = ad.Republished,
    };

    public static AdView ToAdView(Ad ad) => new() {
        Id = ad.Id,
        Source = ad.Source,
        Title = ad.Title,
        Company = ad.Company,
        Location = ad.Location,
        Published = ad.Published,
        Deadline = ad.Deadline,
        Description = ad.Description,
        Label = ad.Label,
        Summary = ad.Summary,
        RequiresMaster = ad.RequiresMaster,
        IsProgrammingJob = ad.IsProgrammingJob,
        DisplayTitle = ad.DisplayTitle,
        RoleType = ad.RoleType,
        Technologies = ad.Technologies,
        Requirements = ad.Requirements,
        Match = MatchingEntry.DecodeMatch(ad.Assessment),
        ApplyUrl = ad.ApplyUrl,
        ApplyStatus = ad.ApplyStatus,
        Republished = ad.Republished,
    };

    public static StoredClassification ToStored(Classification classification) => new(
        classification.Label,
        classification.Summary,
        classification.RequiresMaster,
        classification.IsProgrammingJob,
        classification.DisplayTitle,
        classification.RoleType,
        classification.Technologies,
        classification.Requirements,
        MatchingEntry.EncodeMatch(classification.Match, classification.Checks));

    public static Classification ToClassification(AdView view) => new(
        view.Label ?? AdsEntry.ErrorLabel,
        view.Summary,
        view.RequiresMaster,
        view.IsProgrammingJob == true,
        view.DisplayTitle,
        view.RoleType,
        view.Technologies,
        view.Requirements,
        view.Match,
        []);

    public static ModelPrices ToPrices(ModelSpec spec) =>
        new(spec.Input, spec.Output, spec.CacheRead, spec.CacheWrite5m, spec.CacheWrite1h);

    public static TokenCounts ToTokens(TokenUsage? usage) => usage is null
        ? new TokenCounts(0, 0, 0, 0, 0)
        : new TokenCounts(usage.Input, usage.CacheWrite5m, usage.CacheWrite1h, usage.CacheRead, usage.Output);

    public static DigestCard ToDigestCard(AdView view, string url, Targets targets) => new() {
        Id = view.Id,
        Source = view.Source,
        Title = view.Title,
        DisplayTitle = view.DisplayTitle,
        Company = view.Company,
        Location = view.Location,
        Deadline = view.Deadline,
        DeadlineDate = MatchingEntry.ParseDate(view.Deadline),
        Url = url,
        Summary = view.Summary,
        RoleLabel = CatalogueEntry.Load().RoleLabel(view.RoleType),
        Fit = ToDigestFit(MatchingEntry.DisplayFit(view, targets)),
        IsStrongMatch = MatchingEntry.IsStrongMatch(view),
        ApplyExpired = view.ApplyStatus == expiredStatus,
    };

    public static IReadOnlyList<HiddenCount> ToHiddenCounts(IReadOnlyDictionary<HiddenKind, int> counts) =>
        [.. counts.Select(count => new HiddenCount(ToHiddenGroup(count.Key), count.Value))];

    private static DigestFit ToDigestFit(DisplayFit fit) => fit switch {
        DisplayFit.Strong => DigestFit.Strong,
        DisplayFit.Partial => DigestFit.Partial,
        _ => DigestFit.OnRequest,
    };

    private static HiddenGroup ToHiddenGroup(HiddenKind kind) => kind switch {
        HiddenKind.Senior => HiddenGroup.Senior,
        HiddenKind.Years => HiddenGroup.Years,
        HiddenKind.Masters => HiddenGroup.Masters,
        HiddenKind.Role => HiddenGroup.Role,
        HiddenKind.Place => HiddenGroup.Place,
        _ => HiddenGroup.Other,
    };
}
