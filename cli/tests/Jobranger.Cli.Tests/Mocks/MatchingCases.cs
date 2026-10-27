using System.Text.Json;
using Jobranger.Cli.Features.Matching;

namespace Jobranger.Cli.Tests.Mocks;

internal static class MatchingCases {
    public static IReadOnlyDictionary<string, JsonElement> All() {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(MatchingFixtures.TestData("matching_cases.json")));
        return document.RootElement.EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> Replies() {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(MatchingFixtures.TestData("matching_replies.json")));
        return document.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
    }

    public static AdView Ad(JsonElement item) {
        string id = item.GetProperty("id").GetString()!;
        return new AdView {
            Id = "case:" + id,
            Source = "case",
            Title = item.GetProperty("title").GetString()!,
            Company = "Org One AS",
            Location = "Oslo",
            Description = item.GetProperty("description").GetString(),
        };
    }
}
