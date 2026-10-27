using System.Net;

namespace Jobranger.Cli.Http;

internal sealed class HostLimitHandler(HostLimiter limiter, TimeProvider time) : DelegatingHandler {
    private const string retryAfterHeader = "Retry-After";

    private static readonly TimeSpan rateLimitPenalty = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan failurePenalty = TimeSpan.FromSeconds(10);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        string host = request.RequestUri?.IdnHost ?? "";
        await limiter.AcquireAsync(host, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage response;
        try {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        } catch (HttpRequestException) {
            limiter.Penalize(host, failurePenalty);
            throw;
        } catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested) {
            limiter.Penalize(host, failurePenalty);
            throw new HttpRequestException(HttpRequestError.ConnectionError, error.Message, error);
        }
        if (PenaltyFor(response) is TimeSpan penalty) {
            limiter.Penalize(host, penalty);
        }
        response.Content = await WatchBodyAsync(response.Content, host, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private async Task<HttpContent> WatchBodyAsync(
        HttpContent content, string host, CancellationToken cancellationToken) {
        Stream body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        StreamContent watched = new(new PenalizingStream(body, () => limiter.Penalize(host, failurePenalty)));
        foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers) {
            watched.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return watched;
    }

    private TimeSpan? PenaltyFor(HttpResponseMessage response) {
        bool rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
        if (!rateLimited && response.StatusCode < HttpStatusCode.InternalServerError) {
            return null;
        }
        string? header = response.Headers.TryGetValues(retryAfterHeader, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
        return RetryAfter.Parse(header, time.GetUtcNow()) ?? (rateLimited ? rateLimitPenalty : failurePenalty);
    }
}
