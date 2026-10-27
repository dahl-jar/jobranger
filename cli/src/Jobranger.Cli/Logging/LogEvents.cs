using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Logging;

internal static partial class LogEvents {
    [LoggerMessage(
        EventName = "logs.pruned",
        Level = LogLevel.Information,
        Message = "removed old log files removed={Removed} keep_days={KeepDays}")]
    public static partial void LogsPruned(ILogger logger, int removed, int keepDays);
}
