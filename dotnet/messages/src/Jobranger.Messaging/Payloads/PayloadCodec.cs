using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Jobranger.Messaging.Payloads;

public static class PayloadCodec {
    private const string serializeKeyword = "serialize";

    private static readonly string tagPath = $"{ContractException.RootPath}.{PayloadSchema.TagProperty}";
    private static readonly JsonWriterOptions writerOptions = new() { Encoder = CompactJsonEncoder.Instance };

    public static IPayload DecodePayload(ReadOnlySpan<byte> utf8) {
        JsonPreScan.Check(utf8);
        Utf8JsonReader reader = new(utf8, JsonPreScan.ReaderOptions);
        JsonElement root = JsonElement.ParseValue(ref reader);
        string? tag = TagOf(root);
        PayloadValidator.Validate(root, DefinitionOf(tag), tag);
        try {
            return (IPayload)root.Deserialize(PayloadTypes.ByTag[tag!])!;
        } catch (JsonException error) {
            throw new ContractException(tag, error.Path ?? ContractException.RootPath, FailureKeywords.Type);
        }
    }

    public static T Decode<T>(ReadOnlySpan<byte> utf8) where T : IPayload {
        IPayload payload = DecodePayload(utf8);
        return payload is T typed ? typed : throw new ContractException(payload.Type, tagPath, FailureKeywords.Const);
    }

    public static byte[] Encode(IPayload payload) {
        string definition = DefinitionOf(payload.Type);
        JsonTypeInfo info = PayloadTypes.ByTag[payload.Type];
        if (info.Type != payload.GetType()) {
            throw new ContractException(payload.Type, tagPath, FailureKeywords.Const);
        }
        byte[] utf8 = Serialize(payload, info);
        Utf8JsonReader reader = new(utf8, JsonPreScan.ReaderOptions);
        PayloadValidator.Validate(JsonElement.ParseValue(ref reader), definition, payload.Type);
        return utf8;
    }

    private static byte[] Serialize(IPayload payload, JsonTypeInfo info) {
        ArrayBufferWriter<byte> buffer = new();
        try {
            using Utf8JsonWriter writer = new(buffer, writerOptions);
            JsonSerializer.Serialize(writer, payload, info);
        } catch (Exception error) when (error is ArgumentException or JsonException) {
            throw new ContractException(payload.Type, ContractException.RootPath, serializeKeyword);
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static string DefinitionOf(string? tag) =>
        tag is not null && PayloadSchema.TryGetDefinition(tag, out string? definition)
            ? definition
            : throw new ContractException(tag, tagPath, FailureKeywords.Const);

    private static string? TagOf(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(PayloadSchema.TagProperty, out JsonElement tag)
        && tag.ValueKind == JsonValueKind.String
            ? tag.GetString()
            : null;
}
