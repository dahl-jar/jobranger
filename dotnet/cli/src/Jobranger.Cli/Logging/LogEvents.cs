using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Logging;

internal static partial class LogEvents {
    [LoggerMessage(
        EventName = "logs.pruned",
        Level = LogLevel.Information,
        Message = "removed old log files removed={Removed} keep_days={KeepDays}")]
    public static partial void LogsPruned(ILogger logger, int removed, int keepDays);

    [LoggerMessage(EventName = "source.crashed", Level = LogLevel.Error, Message = "source crashed source={Source}")]
    public static partial void SourceCrashed(ILogger logger, string source, Exception error);

    [LoggerMessage(EventName = "nav.token.failed", Level = LogLevel.Error, Message = "public token request failed")]
    public static partial void NavTokenFailed(ILogger logger, Exception error);

    [LoggerMessage(
        EventName = "nav.feed.failed",
        Level = LogLevel.Error,
        Message = "feed read failed, cursor kept at the last completed page")]
    public static partial void NavFeedFailed(ILogger logger, Exception error);

    [LoggerMessage(
        EventName = "nav.feed.skipped",
        Level = LogLevel.Warning,
        Message = "skipped feed items without a valid uuid skipped={Skipped}")]
    public static partial void NavItemsSkipped(ILogger logger, int skipped);

    [LoggerMessage(EventName = "nav.entry.failed", Level = LogLevel.Warning, Message = "skipped feed entry ad_id={AdId}")]
    public static partial void NavEntryFailed(ILogger logger, string adId, Exception error);

    [LoggerMessage(EventName = "finn.recent.failed", Level = LogLevel.Error, Message = "recent scan failed")]
    public static partial void FinnRecentFailed(ILogger logger, Exception error);

    [LoggerMessage(EventName = "finn.search.failed", Level = LogLevel.Error, Message = "category scan failed")]
    public static partial void FinnSearchFailed(ILogger logger, Exception error);

    [LoggerMessage(EventName = "finn.ad_text.failed", Level = LogLevel.Warning, Message = "ad page read failed ad_id={AdId}")]
    public static partial void FinnAdTextFailed(ILogger logger, string adId, Exception error);

    [LoggerMessage(EventName = "pairing.started", Level = LogLevel.Information, Message = "started pairing")]
    public static partial void PairingStarted(ILogger logger);

    [LoggerMessage(EventName = "pairing.paired", Level = LogLevel.Information, Message = "paired a browser")]
    public static partial void PairingPaired(ILogger logger);

    [LoggerMessage(
        EventName = "pairing.failed",
        Level = LogLevel.Warning,
        Message = "pairing failed failure={Failure}")]
    public static partial void PairingFailed(ILogger logger, string failure);

    [LoggerMessage(
        EventName = "pairing.refused",
        Level = LogLevel.Warning,
        Message = "relay refused pairing status={Status} error_type={ErrorType}")]
    public static partial void PairingRefused(ILogger logger, int status, string errorType);

    [LoggerMessage(
        EventName = "pairing.hub_refused",
        Level = LogLevel.Warning,
        Message = "relay hub refused a pairing frame error_type={ErrorType}")]
    public static partial void PairingHubRefused(ILogger logger, string errorType);

    [LoggerMessage(
        EventName = "ashby.job.skipped",
        Level = LogLevel.Warning,
        Message = "skipped job without id, title, url or listing state source={Source}")]
    public static partial void AshbyJobSkipped(ILogger logger, string source);

    [LoggerMessage(
        EventName = "teamtailor.item.skipped",
        Level = LogLevel.Warning,
        Message = "skipped feed item without guid, title or link source={Source}")]
    public static partial void TeamtailorItemSkipped(ILogger logger, string source);
}
