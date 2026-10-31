using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Sources;

internal static class SourceHttp {
    public static async Task<byte[]> GetAsync(
        HttpClient client, Uri url, SourceRequestOptions options, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        SourceResponse response = await SourceRequests.GetAsync(client, url, options, cancellationToken)
            .ConfigureAwait(false);
        if (response.Status is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices) {
            throw new HttpRequestException(
                $"source answered status={(int)response.Status} host={url.IdnHost}", null, response.Status);
        }
        return response.Body;
    }

    public static bool IsFailure(Exception error, CancellationToken cancellationToken) => error switch {
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        HttpRequestException or IOException or InvalidDataException or JsonException or RefusedRequestException
            or BodyTooLargeException or RegexMatchTimeoutException => true,
        _ => false,
    };
}
