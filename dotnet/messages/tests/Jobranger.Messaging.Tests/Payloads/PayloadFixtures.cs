using System.Text;
using System.Text.Json.Nodes;

namespace Jobranger.Messaging.Tests.Payloads;

public static class PayloadFixtures {
    private static readonly string messages = Path.Combine(AppContext.BaseDirectory, "TestData", "Messages");
    private static readonly string goldenFolder = Path.Combine(messages, "fixtures", "payload");
    private static readonly string invalidFolder = Path.Combine(messages, "fixtures", "payload-invalid");

    public static TheoryData<string> GoldenNames => Names(goldenFolder);

    public static TheoryData<string> InvalidNames => Names(invalidFolder);

    public static JsonObject Schema => JsonNode.Parse(File.ReadAllText(Path.Combine(messages, "payload.schema.json")))!.AsObject();

    public static byte[] Golden(string fileName) => File.ReadAllBytes(Path.Combine(goldenFolder, fileName));

    public static byte[] Invalid(string fileName) => File.ReadAllBytes(Path.Combine(invalidFolder, fileName));

    public static JsonObject GoldenNode(string fileName) => JsonNode.Parse(Golden(fileName))!.AsObject();

    public static byte[] Utf8(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    public static byte[] GoldenWithExtra(string fileName, string extra) {
        string json = Encoding.UTF8.GetString(Golden(fileName));
        return Encoding.UTF8.GetBytes(json.Replace("\"v\": 1,", "\"v\": 1, " + extra, StringComparison.Ordinal));
    }

    private static TheoryData<string> Names(string folder) {
        IEnumerable<string> names = Directory.GetFiles(folder, "*.json")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);
        return new TheoryData<string>(names);
    }
}
