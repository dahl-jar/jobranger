namespace Jobranger.Cli.Http;

internal sealed class ApiSender(
    HttpMessageHandler handler, TimeProvider time, int retries, SdkRules rules, Random random) : IDisposable {
    public const int DefaultRetries = 4;
    public const int MaxBodyBytes = 32 * 1024 * 1024;

    private const string connectionMessage = "Connection error.";
    private const string retryAfterHeader = "retry-after";
    private const string retryYes = "true";
    private const string retryNo = "false";
    private const int requestTimeout = 408;
    private const int conflict = 409;
    private const int tooManyRequests = 429;
    private const int firstServerError = 500;
    private const double firstWaitSeconds = 0.5;
    private const double longestBackoffSeconds = 8;
    private const double jitterShare = 0.25;

    private static readonly TimeSpan attemptTimeout = TimeSpan.FromSeconds(120);

    private readonly HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<ApiReply> SendAsync(Func<HttpRequestMessage> build, CancellationToken cancellationToken) {
        for (int attempt = 0; ; attempt++) {
            bool last = attempt == retries;
            Attempt sent;
            try {
                sent = await AttemptAsync(build, cancellationToken).ConfigureAwait(false);
            } catch (ApiException error) when (error.Kind == ApiErrorKind.Connection && !last) {
                await Task.Delay(Backoff(attempt), time, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (last || sent.Reply.IsSuccess || !ShouldRetry(sent)) {
                return sent.Reply;
            }
            await Task.Delay(WaitBefore(sent, attempt), time, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => http.Dispose();

    private static bool IsRetryable(int status) =>
        status is requestTimeout or conflict or tooManyRequests or >= firstServerError;

    private bool ShouldRetry(Attempt sent) {
        if (rules.RefusesLongerWait && sent.ServerWait is double wait && double.IsFinite(wait)
            && wait > rules.LongestServerWaitSeconds) {
            return false;
        }
        return sent.ShouldRetry switch {
            retryYes => true,
            retryNo => false,
            _ => IsRetryable(sent.Reply.Status),
        };
    }

    private TimeSpan WaitBefore(Attempt sent, int attempt) =>
        sent.ServerWait is double wait && wait > 0
        && (!rules.RefusesLongerWait || wait <= rules.LongestServerWaitSeconds)
            ? TimeSpan.FromSeconds(Math.Min(wait, rules.LongestServerWaitSeconds))
            : Backoff(attempt);

    private TimeSpan Backoff(int attempt) {
        double seconds = Math.Min(firstWaitSeconds * Math.Pow(2, attempt), longestBackoffSeconds);
        return TimeSpan.FromSeconds(seconds * (1 - (jitterShare * random.NextDouble())));
    }

    private async Task<Attempt> AttemptAsync(Func<HttpRequestMessage> build, CancellationToken cancellationToken) {
        using CancellationTokenSource timeout = new(attemptTimeout, time);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using HttpRequestMessage request = build();
        try {
            using HttpResponseMessage response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            Stream body = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            await using (body.ConfigureAwait(false)) {
                byte[] bytes = await CappedReader.ReadAsync(body, MaxBodyBytes, linked.Token).ConfigureAwait(false);
                return new Attempt(
                    new ApiReply((int)response.StatusCode, bytes, RetryAfterOf(response)),
                    ServerWait.Seconds(response.Headers, time.GetUtcNow()),
                    ServerWait.Header(response.Headers, ServerWait.ShouldRetryHeader));
            }
        } catch (OperationCanceledException) when (timeout.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested) {
            throw new ApiException(ApiErrorKind.Connection, rules.TimeoutMessage);
        } catch (Exception error) when (error is HttpRequestException or IOException) {
            throw new ApiException(ApiErrorKind.Connection, connectionMessage);
        } catch (BodyTooLargeException error) {
            throw new ApiException(ApiErrorKind.Server, error.Message);
        }
    }

    private TimeSpan? RetryAfterOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues(retryAfterHeader, out IEnumerable<string>? values)
            ? RetryAfter.Parse(values.FirstOrDefault(), time.GetUtcNow())
            : null;

    private readonly record struct Attempt(ApiReply Reply, double? ServerWait, string? ShouldRetry);
}
