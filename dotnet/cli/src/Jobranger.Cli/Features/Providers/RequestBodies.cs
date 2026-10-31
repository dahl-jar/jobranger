using System.Buffers;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jobranger.Cli.Features.Providers;

internal static class RequestBodies {
    public const int MaxOutputTokens = 4096;

    private const string jsonType = "application/json";

    private static readonly JsonWriterOptions options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static byte[] Write(Action<Utf8JsonWriter> write) {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, options)) {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    public static HttpRequestMessage Post(string url, byte[] body) {
        ByteArrayContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(jsonType);
        HttpRequestMessage request = new(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.ParseAdd(jsonType);
        return request;
    }

    public static HttpRequestMessage Get(string url) {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd(jsonType);
        return request;
    }
}
