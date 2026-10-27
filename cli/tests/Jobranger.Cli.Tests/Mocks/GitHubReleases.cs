using System.Text;
using System.Text.Json.Nodes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class GitHubReleases {
    public const string Highest = "10.0.0";

    private const string stableTag = "cli-v10.0.0";
    private const string tagField = "tag_name";
    private const string bodyField = "body";

    private static readonly string fixture = Path.Combine(AppContext.BaseDirectory, "TestData", "GitHub", "releases.json");

    public static byte[] All() => File.ReadAllBytes(fixture);

    public static byte[] Only(string tag) {
        JsonObject release = Stable();
        release[tagField] = tag;
        return Encoding.UTF8.GetBytes(new JsonArray(release).ToJsonString());
    }

    public static byte[] StableOfSize(int size) {
        JsonObject release = Stable();
        release[bodyField] = "";
        int fixedSize = Encoding.UTF8.GetByteCount(new JsonArray(release.DeepClone()).ToJsonString());
        release[bodyField] = new string('x', size - fixedSize);
        return Encoding.UTF8.GetBytes(new JsonArray(release).ToJsonString());
    }

    private static JsonObject Stable() =>
        JsonNode.Parse(File.ReadAllText(fixture))!.AsArray()
            .Select(release => release!.AsObject())
            .Single(release => (string?)release[tagField] == stableTag)
            .DeepClone().AsObject();
}
