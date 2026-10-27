using Jobranger.Relay.Core;

namespace Jobranger.Relay.Telemetry;

internal static partial class RelayLog
{
    private const string category = "Jobranger.Relay";

    public static ILogger For(ILoggerFactory loggers) => loggers.CreateLogger(category);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "pairing started channel={Channel}")]
    public static partial void PairingStarted(this ILogger logger, [PseudonymousData] string channel);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "pairing claimed channel={Channel}")]
    public static partial void PairingClaimed(this ILogger logger, [PseudonymousData] string channel);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "session started role={Role} channel={Channel}")]
    public static partial void SessionStarted(this ILogger logger, RelayRole role, [PseudonymousData] string channel);

    [LoggerMessage(EventId = 5, Level = LogLevel.Critical, Message = "relay failed to start")]
    public static partial void StartupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "rate limit refused policy={Policy}")]
    public static partial void RateLimitRefused(this ILogger logger, string policy);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "request rejected reason={Reason}")]
    public static partial void RequestRejected(this ILogger logger, string reason);
}
