namespace Jobranger.Cli.Tests.Http;

internal sealed class FakeSourceHandler(Func<int, Uri, HttpResponseMessage> answer) : HttpMessageHandler {
    private readonly List<(Uri Url, IReadOnlyDictionary<string, string> Headers)> seen = [];

    public FakeSourceHandler(Func<int, HttpResponseMessage> answer) : this((index, _) => answer(index)) {
    }

    public IReadOnlyList<Uri> Urls => [.. seen.Select(request => request.Url)];

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Headers => [.. seen.Select(request => request.Headers)];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        Dictionary<string, string> headers = request.Headers.ToDictionary(
            header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
        Uri url = request.RequestUri!;
        seen.Add((url, headers));
        return Task.FromResult(answer(seen.Count - 1, url));
    }
}
