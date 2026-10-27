using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Fields;
using Jobranger.Contracts.Limits;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Sessions;

internal static class Sessions {
    private const int sidBytes = 16;

    public static CliHandshake Accept(Key cliStatic, string sid, HelloSteps.Commit commit) {
        if (!CanonicalBase64Url.TryDecode(sid, sidBytes, out byte[] sidRaw)) {
            throw new HandshakeException(HandshakeFailure.Step);
        }
        return new CliHandshake(cliStatic, sidRaw, commit, Key.Create(KeyAgreementAlgorithm.X25519));
    }

    public static byte[] PairTag(string code, ChannelId channel) {
        byte[] message = [.. Encoding.UTF8.GetBytes(ContractLimits.PairTagLabel), .. Base64Url.DecodeFromChars(channel.Value)];
        byte[] mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), message);
        return mac[..ContractLimits.PairTagBytes];
    }

    public static bool IsPairTag(string code, ChannelId channel, ReadOnlySpan<byte> pairTag) =>
        CryptographicOperations.FixedTimeEquals(PairTag(code, channel), pairTag);
}
