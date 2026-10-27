using System.Buffers.Text;
using Jobranger.Contracts.Channels;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Link;

internal sealed record LinkKeyPair(Key Signing, Key Agreement) : IDisposable {
    public ChannelId Channel => ChannelId.FromPublicKey(Signing.PublicKey.Export(KeyBlobFormat.RawPublicKey));

    public string AgreementPublicKey => Base64Url.EncodeToString(Agreement.PublicKey.Export(KeyBlobFormat.RawPublicKey));

    public void Dispose() {
        Signing.Dispose();
        Agreement.Dispose();
    }
}
