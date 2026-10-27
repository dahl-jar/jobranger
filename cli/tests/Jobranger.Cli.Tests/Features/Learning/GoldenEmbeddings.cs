using System.Text.Json;
using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

internal static class GoldenEmbeddings {
    private static readonly string path =
        Path.Combine(AppContext.BaseDirectory, "Golden", "Embeddings", "embeddings.json");

    private static readonly Lazy<JsonDocument> document = new(() => JsonDocument.Parse(File.ReadAllBytes(path)));

    public static IReadOnlyList<GoldenCase> Cases() =>
        [.. document.Value.RootElement.GetProperty("cases").EnumerateArray().Select(ToCase)];

    public static IReadOnlyList<ModelFile> Files() =>
        [.. document.Value.RootElement.GetProperty("files").EnumerateArray().Select(ToFile)];

    private static GoldenCase ToCase(JsonElement element) =>
        new(
            element.GetProperty("text").GetString()!,
            [.. element.GetProperty("ids").EnumerateArray().Select(id => id.GetInt32())],
            [.. element.GetProperty("embedding").EnumerateArray().Select(value => (float)value.GetDouble())]);

    private static ModelFile ToFile(JsonElement element) =>
        new(
            element.GetProperty("name").GetString()!,
            new Uri(element.GetProperty("url").GetString()!),
            element.GetProperty("sha256").GetString()!,
            element.GetProperty("size").GetInt64());
}
