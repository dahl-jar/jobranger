using System.Buffers.Text;
using System.Security.Cryptography;

namespace Jobranger.Relay.Core;

public readonly record struct ChannelId(string Value)
{
    private static ReadOnlySpan<byte> Prefix => "jobranger-chan"u8;

    public static ChannelId FromPublicKey(ReadOnlySpan<byte> publicKey) =>
        new(Base64Url.EncodeToString(SHA256.HashData([.. Prefix, .. publicKey])));
}
