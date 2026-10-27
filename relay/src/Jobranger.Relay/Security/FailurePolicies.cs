namespace Jobranger.Relay.Security;

internal sealed class FailurePolicies(EndpointDataSource endpoints) {
    private readonly Lazy<Dictionary<string, string>> byRoute = new(() => endpoints.Endpoints
        .OfType<RouteEndpoint>()
        .SelectMany(endpoint =>
            endpoint.Metadata.GetMetadata<RelayFailureLimit>() is { } limit && endpoint.RoutePattern.RawText is { } route
                ? [KeyValuePair.Create(route, limit.Policy)]
                : Array.Empty<KeyValuePair<string, string>>())
        .ToDictionary(StringComparer.OrdinalIgnoreCase));

    public string? For(HttpContext http) =>
        http.GetEndpoint()?.Metadata.GetMetadata<RelayFailureLimit>()?.Policy
        ?? byRoute.Value.GetValueOrDefault(http.Request.Path.Value ?? string.Empty);
}
