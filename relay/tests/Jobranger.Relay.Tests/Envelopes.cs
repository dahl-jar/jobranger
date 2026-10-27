using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core;

namespace Jobranger.Relay.Tests;

internal static class Envelopes {
    private const int sidBytes = 16;
    private const int unsupportedVersion = 2;

    private static readonly string folder = Path.Combine(AppContext.BaseDirectory, "contracts", "fixtures", "envelope");

    public static string NewSid() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(sidBytes));

    public static JsonObject Data(ChannelId channel, string sid) => Load("data.json", channel, sid);

    public static JsonObject Hello(ChannelId channel, string sid) => Load("hello.json", channel, sid);

    public static JsonObject WithUnsupportedVersion(ChannelId channel) {
        JsonObject envelope = Data(channel, NewSid());
        envelope["v"] = unsupportedVersion;
        return envelope;
    }

    public static string Compact(string json) => JsonNode.Parse(json)!.ToJsonString();

    private static JsonObject Load(string fileName, ChannelId channel, string sid) {
        JsonObject envelope = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, fileName)))!.AsObject();
        envelope["channel"] = channel.Value;
        envelope["sid"] = sid;
        return envelope;
    }
}
