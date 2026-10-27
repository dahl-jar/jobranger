using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;
using Jobranger.Contracts.Limits;

namespace Jobranger.Contracts.Payloads;

internal static class JsonPreScan {
    private const string jsonKeyword = "json";
    private const string depthKeyword = "depth";
    private const string duplicateKeyword = "duplicate_key";
    private const string surrogateKeyword = "surrogate";
    private const string digitsKeyword = "integer_digits";
    private const string finiteKeyword = "non_finite";
    private const byte minus = (byte)'-';

    private static readonly SearchValues<byte> fractionOrExponent = SearchValues.Create(".eE"u8);

    public static JsonReaderOptions ReaderOptions { get; } = new() { MaxDepth = ContractLimits.MaxJsonDepth + 1 };

    public static void Check(ReadOnlySpan<byte> utf8) {
        if (!Utf8.IsValid(utf8)) {
            throw ContractException.AtRoot(FailureKeywords.Utf8);
        }
        Utf8JsonReader reader = new(utf8, ReaderOptions);
        Stack<HashSet<string>> objects = new();
        try {
            while (reader.Read()) {
                CheckToken(ref reader, objects);
            }
        } catch (JsonException) {
            throw ContractException.AtRoot(jsonKeyword);
        }
    }

    private static void CheckToken(ref Utf8JsonReader reader, Stack<HashSet<string>> objects) {
        switch (reader.TokenType) {
            case JsonTokenType.StartObject:
                CheckDepth(ref reader);
                objects.Push(new HashSet<string>(StringComparer.Ordinal));
                break;
            case JsonTokenType.StartArray:
                CheckDepth(ref reader);
                break;
            case JsonTokenType.EndObject:
                _ = objects.Pop();
                break;
            case JsonTokenType.PropertyName:
                if (!objects.Peek().Add(Text(ref reader))) {
                    throw ContractException.AtRoot(duplicateKeyword);
                }
                break;
            case JsonTokenType.String:
                if (reader.ValueIsEscaped) {
                    _ = Text(ref reader);
                }
                break;
            case JsonTokenType.Number:
                CheckNumber(ref reader);
                break;
            default:
                break;
        }
    }

    private static void CheckDepth(ref Utf8JsonReader reader) {
        if (reader.CurrentDepth >= ContractLimits.MaxJsonDepth) {
            throw ContractException.AtRoot(depthKeyword);
        }
    }

    private static string Text(ref Utf8JsonReader reader) {
        try {
            return reader.GetString()!;
        } catch (InvalidOperationException) {
            throw ContractException.AtRoot(surrogateKeyword);
        }
    }

    private static void CheckNumber(ref Utf8JsonReader reader) {
        ReadOnlySpan<byte> number = reader.ValueSpan;
        if (!number.ContainsAny(fractionOrExponent) && Digits(number) > ContractLimits.MaxIntegerDigits) {
            throw ContractException.AtRoot(digitsKeyword);
        }
        if (!reader.TryGetDouble(out double value) || !double.IsFinite(value)) {
            throw ContractException.AtRoot(finiteKeyword);
        }
    }

    private static int Digits(ReadOnlySpan<byte> number) => number.Length - (number[0] == minus ? 1 : 0);
}
