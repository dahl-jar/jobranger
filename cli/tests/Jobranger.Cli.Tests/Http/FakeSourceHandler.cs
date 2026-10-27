namespace Jobranger.Cli.Tests.Http;

internal sealed class FakeSourceHandler(Func<int, HttpResponseMessage> answer) : HttpMessageHandler {
    private readonly List<(Uri Url, IReadOnlyDictionary<string, string> Headers)> seen = [];

    public IReadOnlyList<Uri> Urls => [.. seen.Select(request => request.Url)];

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Headers => [.. seen.Select(request => request.Headers)];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        Dictionary<string, string> headers = request.Headers.ToDictionary(
            header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
        seen.Add((request.RequestUri!, headers));
        return Task.FromResult(answer(seen.Count - 1));
    }
}
