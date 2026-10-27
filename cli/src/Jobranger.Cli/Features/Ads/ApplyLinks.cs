using System.Collections.Frozen;
using System.Net;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Ads;

internal static class ApplyLinks {
    private const string finnApplyPathPrefix = "/job-apply/";
    private const string theHubDomain = "thehub.io";
    private const char domainSeparator = '.';

    private static readonly TimeSpan checkDeadline = TimeSpan.FromSeconds(30);

    private static readonly SourceRequestOptions options = new() { MaxAttempts = 1 };

    private static readonly FrozenSet<string> finnHosts =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "www.finn.no", "finn.no");

    private static readonly FrozenSet<string> nativeApplyHosts =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, [.. finnHosts, "login.vend.no"]);

    private static readonly FrozenSet<HttpStatusCode> expiredStatuses = [HttpStatusCode.NotFound, HttpStatusCode.Gone];

    private static readonly (string Domain, byte[][] Markers)[] expiredMarkers = [
        (theHubDomain, ["status:\"EXPIRED\""u8.ToArray(), "status:\"STOPPED\""u8.ToArray()]),
    ];

    public static async Task<ApplyStatus> CheckAsync(
        HttpClient client, string url, TimeProvider time, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? start) || !IsCheckable(start)) {
            return ApplyStatus.Skipped;
        }
        using CancellationTokenSource deadline = new(checkDeadline, time);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            SourceResponse response = await SourceRequests
                .GetAsync(client, start, options with { Time = time }, linked.Token)
                .ConfigureAwait(false);
            return StatusOf(response);
        } catch (Exception error) when (IsRefused(error)) {
            return ApplyStatus.Blocked;
        } catch (Exception error) when (IsUnreachable(error, cancellationToken)) {
            return ApplyStatus.Unknown;
        }
    }

    private static bool IsCheckable(Uri url) {
        if (!SourceRequests.IsWebScheme(url)) {
            return false;
        }
        return !finnHosts.Contains(url.Host)
            || url.AbsolutePath.StartsWith(finnApplyPathPrefix, StringComparison.Ordinal);
    }

    private static ApplyStatus StatusOf(SourceResponse response) {
        int code = (int)response.Status;
        if (expiredStatuses.Contains(response.Status)) {
            return ApplyStatus.Expired;
        }
        if (code >= (int)HttpStatusCode.BadRequest) {
            return ApplyStatus.Unknown;
        }
        if (code >= (int)HttpStatusCode.MultipleChoices) {
            return ApplyStatus.Blocked;
        }
        string host = response.Url.Host;
        if (nativeApplyHosts.Contains(host)) {
            return ApplyStatus.Skipped;
        }
        return MarkersFor(host).Any(marker => response.Body.AsSpan().IndexOf(marker) >= 0)
            ? ApplyStatus.Expired
            : ApplyStatus.Active;
    }

    private static byte[][] MarkersFor(string host) {
        foreach ((string domain, byte[][] markers) in expiredMarkers) {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(domainSeparator + domain, StringComparison.OrdinalIgnoreCase)) {
                return markers;
            }
        }
        return [];
    }

    private static bool IsRefused(Exception error) => error switch {
        RefusedRequestException => true,
        HttpRequestException request => request.InnerException is RefusedRequestException,
        _ => false,
    };

    private static bool IsUnreachable(Exception error, CancellationToken cancellationToken) => error switch {
        HttpRequestException or IOException or BodyTooLargeException => true,
        InvalidDataException or InvalidOperationException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };
}
