using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeApiHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
    : HttpMessageHandler {
    private const string jsonType = "application/json";

    private readonly SemaphoreSlim arrivals = new(0);

    public List<SentRequest> Requests { get; } = [];

    public static FakeApiHandler Json(int status, string body, params (string Name, string Value)[] headers) =>
        new((request, cancellationToken) => Task.FromResult(Response(status, body, headers)));

    public static FakeApiHandler Text(int status, string body) =>
        new((request, cancellationToken) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) {
            Content = new StringContent(body, Encoding.UTF8),
        }));

    public static FakeApiHandler Sequence(params Func<HttpResponseMessage>[] replies) {
        int next = 0;
        return new((request, cancellationToken) =>
            Task.FromResult(replies[Math.Min(Interlocked.Increment(ref next) - 1, replies.Length - 1)]()));
    }

    public static FakeApiHandler Refusing() =>
        new((request, cancellationToken) => throw new HttpRequestException("connection refused"));

    public static FakeApiHandler Hanging() =>
        new(async (request, cancellationToken) => {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("hanging handler answered");
        });

    public static HttpResponseMessage Response(int status, string body, params (string Name, string Value)[] headers) {
        HttpResponseMessage response = new((HttpStatusCode)status) {
            Content = new StringContent(body, Encoding.UTF8, jsonType),
        };
        foreach ((string name, string value) in headers) {
            response.Headers.TryAddWithoutValidation(name, value);
        }
        return response;
    }

    public SentRequest Last => Requests[^1];

    public Task<bool> ArrivedAsync(TimeSpan wait) => arrivals.WaitAsync(wait, TestContext.Current.CancellationToken);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        byte[] body = request.Content is null
            ? []
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        Copy(request.Headers, headers);
        if (request.Content is not null) {
            Copy(request.Content.Headers, headers);
        }
        lock (Requests) {
            Requests.Add(new SentRequest(request.RequestUri!, headers, body));
        }
        arrivals.Release();
        return await reply(request, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            arrivals.Dispose();
        }
        base.Dispose(disposing);
    }

    private static void Copy(HttpHeaders source, Dictionary<string, string> target) {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source) {
            target[header.Key] = string.Join(", ", header.Value);
        }
    }
}
