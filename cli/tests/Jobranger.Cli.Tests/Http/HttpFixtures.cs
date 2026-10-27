using System.Net;

namespace Jobranger.Cli.Tests.Http;

internal static class HttpFixtures {
    private const string locationHeader = "Location";

    public static HttpResponseMessage RawRedirect(string location) {
        HttpResponseMessage response = new(HttpStatusCode.Found);
        response.Headers.TryAddWithoutValidation(locationHeader, location);
        return response;
    }

    public static TaskCanceledException ConnectTimeout() => new("connect timed out", new TimeoutException());
}
