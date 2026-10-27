using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Jobranger.Cli.Http;

internal static class SourceRequests {
    private const int maxRedirects = 5;
    private const string locationHeader = "Location";

    private static readonly TimeSpan attemptDeadline = TimeSpan.FromSeconds(120);

    private static readonly FrozenSet<string> webSchemes =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, Uri.UriSchemeHttp, Uri.UriSchemeHttps);

    private static readonly FrozenSet<HttpStatusCode> retryStatuses = [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private static readonly FrozenSet<HttpStatusCode> redirectStatuses = [
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.Found,
        HttpStatusCode.SeeOther,
        HttpStatusCode.TemporaryRedirect,
        HttpStatusCode.PermanentRedirect,
    ];

    private static readonly FrozenSet<HttpRequestError> transientErrors = [
        HttpRequestError.ConnectionError,
        HttpRequestError.NameResolutionError,
    ];

    public static async Task<SourceResponse> GetAsync(
        HttpClient client, Uri url, SourceRequestOptions options, CancellationToken cancellationToken) {
        for (int attempt = 1; ; attempt++) {
            bool last = attempt >= options.MaxAttempts;
            try {
                SourceResponse response = await FollowAsync(client, url, options, cancellationToken).ConfigureAwait(false);
                if (last || !retryStatuses.Contains(response.Status)) {
                    return response;
                }
            } catch (Exception error) when (!last && IsTransient(error)) {
            }
        }
    }

    private static async Task<SourceResponse> FollowAsync(
        HttpClient client, Uri url, SourceRequestOptions options, CancellationToken cancellationToken) {
        using CancellationTokenSource deadline = new(attemptDeadline, options.Time);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        HostLimitHandler.SlotDeadline slotDeadline = new(deadline, attemptDeadline);
        try {
            return await FollowHopsAsync(client, url, options, slotDeadline, linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested) {
            throw new IOException(string.Create(
                CultureInfo.InvariantCulture, $"attempt passed {attemptDeadline.TotalSeconds} s host={url.IdnHost}"));
        }
    }

    private static async Task<SourceResponse> FollowHopsAsync(
        HttpClient client, Uri url, SourceRequestOptions options, HostLimitHandler.SlotDeadline deadline,
        CancellationToken cancellationToken) {
        Uri current = url;
        for (int hop = 0; hop <= maxRedirects; hop++) {
            Check(current);
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            request.Options.Set(HostLimitHandler.DeadlineKey, deadline);
            if (IsSameOrigin(current, url)) {
                foreach (KeyValuePair<string, string> header in options.Headers) {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (redirectStatuses.Contains(response.StatusCode) && response.Headers.Contains(locationHeader)) {
                current = NextHop(current, response.Headers);
                continue;
            }
            Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false)) {
                byte[] bytes = await CappedReader.ReadAsync(body, options.MaxBodyBytes, cancellationToken)
                    .ConfigureAwait(false);
                return new SourceResponse(response.StatusCode, current, bytes, response.Content.Headers.ContentType?.CharSet);
            }
        }
        throw new RefusedRequestException($"more than {maxRedirects} redirects host={url.IdnHost}");
    }

    private static Uri NextHop(Uri current, HttpResponseHeaders headers) => headers.Location switch {
        { IsAbsoluteUri: true } location => location,
        Uri location when Uri.TryCreate(current, location, out Uri? next) => next,
        _ => throw new RefusedRequestException(
            $"unreadable redirect location={headers.NonValidated[locationHeader]}"),
    };

    internal static bool IsWebScheme(Uri url) => webSchemes.Contains(url.Scheme);

    private static void Check(Uri url) {
        if (!url.IsAbsoluteUri) {
            throw new RefusedRequestException("relative url");
        }
        if (!IsWebScheme(url)) {
            throw new RefusedRequestException($"unsupported scheme scheme={url.Scheme}");
        }
        if (url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && !PublicAddress.IsPublic(IPAddress.Parse(url.DnsSafeHost))) {
            throw new RefusedRequestException($"private address host={url.IdnHost}");
        }
    }

    internal static bool IsSameOrigin(Uri left, Uri right) =>
        Uri.Compare(left, right, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool IsTransient(Exception error) => error switch {
        HttpRequestException request =>
            request.InnerException is not RefusedRequestException
            && (transientErrors.Contains(request.HttpRequestError) || IsReadTimeout(request)),
        IOException => true,
        _ => false,
    };

    private static bool IsReadTimeout(HttpRequestException error) =>
        error.HttpRequestError == HttpRequestError.Unknown && error.InnerException is IOException;
}
