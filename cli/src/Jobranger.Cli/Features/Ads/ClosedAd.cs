namespace Jobranger.Cli.Features.Ads;

internal sealed record ClosedAd(Ad Ad, string ClosedOn, ClosedReason Reason, AdAction? Action);
