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

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "hub connection refused reason={Reason} channel={Channel}")]
    public static partial void HubConnectionRefused(this ILogger logger, [PseudonymousData] string channel, string reason);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "hub auth refused reason={Reason}")]
    public static partial void HubAuthRefused(this ILogger logger, string reason);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "hub send timed out method={Method}")]
    public static partial void HubSendTimedOut(this ILogger logger, string method);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "hub step failed step={Step}")]
    public static partial void HubFailed(this ILogger logger, Exception exception, string step);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "envelope rejected reason={Reason} channel={Channel}")]
    public static partial void EnvelopeRejected(this ILogger logger, [PseudonymousData] string channel, string reason);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "request rejected reason={Reason}")]
    public static partial void RequestRejected(this ILogger logger, string reason);
}
