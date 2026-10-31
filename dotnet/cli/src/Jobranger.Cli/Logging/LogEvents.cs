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

    [LoggerMessage(
        EventName = "source.deadline_passed",
        Level = LogLevel.Warning,
        Message = "source passed its deadline, kept ads source={Source} count={Count}")]
    public static partial void SourceDeadlinePassed(ILogger logger, string source, int count);

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
        EventName = "report.failed",
        Level = LogLevel.Warning,
        Message = "report failed failure={Failure}")]
    public static partial void ReportFailed(ILogger logger, string failure);

    [LoggerMessage(
        EventName = "logs.failed",
        Level = LogLevel.Warning,
        Message = "logs failed failure={Failure}")]
    public static partial void LogsFailed(ILogger logger, string failure);

    [LoggerMessage(
        EventName = "settings.read_failed",
        Level = LogLevel.Warning,
        Message = "settings read failed failure={Failure}")]
    public static partial void SettingsReadFailed(ILogger logger, string failure);

    [LoggerMessage(EventName = "edit.failed", Level = LogLevel.Warning, Message = "edit failed failure={Failure}")]
    public static partial void EditFailed(ILogger logger, string failure);

    [LoggerMessage(EventName = "edit.cleanup_failed", Level = LogLevel.Warning, Message = "edit folder not removed")]
    public static partial void EditCleanupFailed(ILogger logger);

    [LoggerMessage(
        EventName = "recheck.skipped",
        Level = LogLevel.Warning,
        Message = "recheck estimate skipped error_type={ErrorType}")]
    public static partial void RecheckSkipped(ILogger logger, string errorType);

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

    [LoggerMessage(EventName = "run.locked", Level = LogLevel.Information, Message = "another run holds the run lock")]
    public static partial void RunLocked(ILogger logger);

    [LoggerMessage(
        EventName = "classify.outdated",
        Level = LogLevel.Information,
        Message = "queued ads assessed with an older prompt count={Count}")]
    public static partial void OutdatedQueued(ILogger logger, int count);

    [LoggerMessage(
        EventName = "classify.call_retried",
        Level = LogLevel.Warning,
        Message = "model call failed, ad queued again failure={Failure}")]
    public static partial void ClassifyCallRetried(ILogger logger, string failure);

    [LoggerMessage(
        EventName = "classify.budget_paused",
        Level = LogLevel.Information,
        Message = "stopped model calls at the monthly limit")]
    public static partial void ClassifyBudgetPaused(ILogger logger);

    [LoggerMessage(
        EventName = "classify.ad_over_limit",
        Level = LogLevel.Warning,
        Message = "skipped an ad whose model call costs more than the monthly limit ad_id={AdId}")]
    public static partial void ClassifyAdOverLimit(ILogger logger, string adId);

    [LoggerMessage(
        EventName = "classify.provider.failed",
        Level = LogLevel.Warning,
        Message = "model call failed, classification stopped failure={Failure}")]
    public static partial void ClassifyProviderFailed(ILogger logger, string failure);

    [LoggerMessage(
        EventName = "classify.spend.failed",
        Level = LogLevel.Error,
        Message = "could not record spend of a finished model call")]
    public static partial void ClassifySpendFailed(ILogger logger, Exception error);

    [LoggerMessage(
        EventName = "apply.check.done",
        Level = LogLevel.Information,
        Message = "checked apply links checked={Checked} expired={Expired} unknown={Unknown} skipped={Skipped} blocked={Blocked}")]
    public static partial void ApplyChecksDone(ILogger logger, int @checked, int expired, int unknown, int skipped, int blocked);

    [LoggerMessage(
        EventName = "apply.veto",
        Level = LogLevel.Information,
        Message = "expired an ad missing from its employer feed ad_id={AdId} source={Source}")]
    public static partial void ApplyVetoed(ILogger logger, string adId, string source);
}
