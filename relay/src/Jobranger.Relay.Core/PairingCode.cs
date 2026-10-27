using System.Buffers;
using System.Security.Cryptography;

namespace Jobranger.Relay.Core;

public readonly record struct PairingCode(string Selector, string Verifier) {
    private const string alphabet = "BCDFGHJKLMNPQRSTVWXZ";

    private const int partLength = 4;
    private const int codeLength = partLength * 2;

    private static readonly SearchValues<char> alphabetChars = SearchValues.Create(alphabet);

    public static PairingCode Generate() => Split(RandomNumberGenerator.GetString(alphabet, codeLength));

    public static bool TryParse(string? input, out PairingCode code) {
        code = default;
        if (input is null) {
            return false;
        }

        string compact = input
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (compact.Length != codeLength || compact.AsSpan().ContainsAnyExcept(alphabetChars)) {
            return false;
        }

        code = Split(compact);
        return true;
    }

    public override string ToString() => $"{Selector}-{Verifier}";

    private static PairingCode Split(string compact) => new(compact[..partLength], compact[partLength..]);
}
