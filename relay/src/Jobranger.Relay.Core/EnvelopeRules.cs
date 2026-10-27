using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Jobranger.Relay.Core;

public static class EnvelopeRules {
    private const int envelopeVersion = 1;
    private const long maxSeq = uint.MaxValue;
    private const int channelLength = 43;
    private const int sidLength = 22;
    private const int helloMinChars = 4;
    private const int base64Quantum = 4;
    private const int bytesPerQuantum = 3;
    private const int ceilingAddend = bytesPerQuantum - 1;
    private const string helloKind = "hello";
    private const string dataKind = "data";

    public static bool TryParse(ReadOnlySpan<byte> utf8, [NotNullWhen(true)] out Envelope? envelope) {
        envelope = null;
        Envelope? parsed;
        try {
            if (!HasSafeTokens(utf8)) {
                return false;
            }
            parsed = JsonSerializer.Deserialize(utf8, EnvelopeJsonContext.Default.Envelope);
        } catch (JsonException) {
            return false;
        }
        if (parsed is null || !IsValid(parsed)) {
            return false;
        }
        envelope = parsed;
        return true;
    }

    private static bool IsValid(Envelope envelope) {
        return envelope.V == envelopeVersion
            && IsCanonicalBase64Url(envelope.Channel, channelLength, channelLength)
            && IsCanonicalBase64Url(envelope.Sid, sidLength, sidLength)
            && HasValidKindFields(envelope);
    }

    private static bool HasValidKindFields(Envelope envelope) => envelope.Kind switch {
        helloKind => envelope.Seq == 0
            && IsCanonicalBase64Url(
                envelope.Ciphertext,
                helloMinChars,
                Base64UrlChars(ContractLimits.HelloMaxBytes)),
        dataKind => envelope.Seq is >= 1 and <= maxSeq
            && IsCanonicalBase64Url(
                envelope.Ciphertext,
                Base64UrlChars(ContractLimits.GcmTagBytes),
                ContractLimits.CiphertextB64MaxChars),
        _ => false,
    };

    private static int Base64UrlChars(int bytes) => ((bytes * base64Quantum) + ceilingAddend) / bytesPerQuantum;

    private static bool IsCanonicalBase64Url(string text, int minLength, int maxLength) =>
        text.Length >= minLength && text.Length <= maxLength && CanonicalBase64Url.IsCanonical(text);

    private static bool HasSafeTokens(ReadOnlySpan<byte> utf8) {
        Utf8JsonReader reader = new(utf8, new JsonReaderOptions { MaxDepth = ContractLimits.MaxJsonDepth });
        Stack<HashSet<string>> objects = new();
        try {
            while (reader.Read()) {
                if (!IsSafeToken(ref reader, objects)) {
                    return false;
                }
            }
        } catch (InvalidOperationException) {
            return false;
        }
        return true;
    }

    private static bool IsSafeToken(ref Utf8JsonReader reader, Stack<HashSet<string>> objects) {
        switch (reader.TokenType) {
            case JsonTokenType.StartObject:
                objects.Push(new HashSet<string>(StringComparer.Ordinal));
                return true;
            case JsonTokenType.EndObject:
                _ = objects.Pop();
                return true;
            case JsonTokenType.PropertyName:
                return objects.Peek().Add(reader.GetString()!);
            case JsonTokenType.String:
                _ = reader.GetString();
                return true;
            case JsonTokenType.Number:
                return HasShortIntegerPart(reader.ValueSpan) && reader.TryGetDouble(out double value) && double.IsFinite(value);
            default:
                return true;
        }
    }

    private static bool HasShortIntegerPart(ReadOnlySpan<byte> number) {
        if (number.ContainsAny(".eE"u8)) {
            return true;
        }
        int digits = number.Length - (number[0] == (byte)'-' ? 1 : 0);
        return digits <= ContractLimits.MaxIntegerDigits;
    }
}
