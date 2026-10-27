using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class SiteRoutes {
    private const string urlKey = "url";
    private const string statusKey = "status";
    private const string typeKey = "type";
    private const string fileKey = "file";

    public static FakeSiteHandler Load(string routesPath) {
        string root = Path.GetDirectoryName(routesPath)!;
        Dictionary<string, JsonNode> routes = JsonNode.Parse(File.ReadAllText(routesPath))!.AsArray()
            .OfType<JsonNode>()
            .ToDictionary(route => RouteKey(new Uri(route[urlKey]!.GetValue<string>())), StringComparer.Ordinal);
        return new FakeSiteHandler(url =>
            routes.TryGetValue(url.GetLeftPart(UriPartial.Query), out JsonNode? route)
            || routes.TryGetValue(url.GetLeftPart(UriPartial.Path), out route)
                ? Answer(route, root)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static string RouteKey(Uri url) =>
        url.Query.Length > 0 ? url.GetLeftPart(UriPartial.Query) : url.GetLeftPart(UriPartial.Path);

    private static HttpResponseMessage Answer(JsonNode route, string root) {
        ByteArrayContent content = new(File.ReadAllBytes(Path.Combine(root, route[fileKey]!.GetValue<string>())));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(route[typeKey]!.GetValue<string>());
        return new HttpResponseMessage((HttpStatusCode)route[statusKey]!.GetValue<int>()) { Content = content };
    }
}
