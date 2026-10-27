using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jobranger.Contracts.Fields;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Link;

internal static class LinkKeys {
    public const string SecretName = "link-keys";
    public const string UnpairHint = "run `jobranger unpair` to make new ones";

    private const string signingField = "signing";
    private const string agreementField = "agreement";
    private const int keyBytes = 32;

    public static string NewPair() {
        byte[] signing = RandomNumberGenerator.GetBytes(keyBytes);
        byte[] agreement = RandomNumberGenerator.GetBytes(keyBytes);
        using MemoryStream content = new();
        try {
            using (Utf8JsonWriter writer = new(content)) {
                writer.WriteStartObject();
                writer.WriteString(signingField, Base64Url.EncodeToString(signing));
                writer.WriteString(agreementField, Base64Url.EncodeToString(agreement));
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(content.GetBuffer(), 0, (int)content.Length);
        } finally {
            CryptographicOperations.ZeroMemory(signing);
            CryptographicOperations.ZeroMemory(agreement);
            CryptographicOperations.ZeroMemory(content.GetBuffer());
        }
    }

    public static LinkKeyPair Parse(string stored) {
        using JsonDocument pair = ParseObject(stored);
        Key signing = Import(SignatureAlgorithm.Ed25519, pair.RootElement, signingField);
        try {
            return new LinkKeyPair(signing, Import(KeyAgreementAlgorithm.X25519, pair.RootElement, agreementField));
        } catch {
            signing.Dispose();
            throw;
        }
    }

    private static JsonDocument ParseObject(string stored) {
        string notObject = $"stored link keys are not a JSON object name={SecretName}";
        JsonDocument pair;
        try {
            pair = JsonDocument.Parse(stored);
        } catch (JsonException) {
            throw new LinkKeyException(notObject);
        }
        if (pair.RootElement.ValueKind != JsonValueKind.Object) {
            pair.Dispose();
            throw new LinkKeyException(notObject);
        }
        return pair;
    }

    private static Key Import(Algorithm algorithm, JsonElement pair, string field) {
        byte[] raw = [];
        bool valid = pair.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && CanonicalBase64Url.TryDecode(value.GetString(), keyBytes, out raw);
        try {
            return valid
                ? Key.Import(algorithm, raw, KeyBlobFormat.RawPrivateKey)
                : throw new LinkKeyException(
                    $"stored link key is not {keyBytes} base64url bytes name={SecretName} field={field}");
        } finally {
            CryptographicOperations.ZeroMemory(raw);
        }
    }
}
