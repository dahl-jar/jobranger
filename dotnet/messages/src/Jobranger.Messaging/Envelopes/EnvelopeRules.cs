using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Limits;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Envelopes;

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
            JsonPreScan.Check(utf8);
            parsed = JsonSerializer.Deserialize(utf8, MessagesJsonContext.Default.Envelope);
        } catch (Exception error) when (error is JsonException or ContractException) {
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
                Base64UrlChars(MessageLimits.HelloMaxBytes)),
        dataKind => envelope.Seq is >= 1 and <= maxSeq
            && IsCanonicalBase64Url(
                envelope.Ciphertext,
                Base64UrlChars(MessageLimits.GcmTagBytes),
                MessageLimits.CiphertextB64MaxChars),
        _ => false,
    };

    private static int Base64UrlChars(int bytes) => ((bytes * base64Quantum) + ceilingAddend) / bytesPerQuantum;

    private static bool IsCanonicalBase64Url(string text, int minLength, int maxLength) =>
        text.Length >= minLength && text.Length <= maxLength && CanonicalBase64Url.IsCanonical(text);
}
