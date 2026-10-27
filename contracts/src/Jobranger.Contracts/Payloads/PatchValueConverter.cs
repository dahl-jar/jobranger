using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Jobranger.Contracts.Payloads;

public sealed class PatchValueConverter<T> : JsonConverter<PatchValue<T?>> {
    public override PatchValue<T?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        JsonTypeInfo<T> info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        return new PatchValue<T?>(JsonSerializer.Deserialize(ref reader, info));
    }

    public override void Write(Utf8JsonWriter writer, PatchValue<T?> value, JsonSerializerOptions options) {
        JsonTypeInfo<T?> info = (JsonTypeInfo<T?>)options.GetTypeInfo(typeof(T));
        JsonSerializer.Serialize(writer, value.Value, info);
    }
}
