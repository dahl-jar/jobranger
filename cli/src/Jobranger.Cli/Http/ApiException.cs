namespace Jobranger.Cli.Http;

internal sealed class ApiException(ApiErrorKind kind, string message, TimeSpan? retryAfter = null)
    : Exception(message) {
    public ApiErrorKind Kind { get; } = kind;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
