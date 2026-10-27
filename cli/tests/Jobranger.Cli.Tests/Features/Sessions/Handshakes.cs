using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Tests.Mocks;
using NSec.Cryptography;
using SessionsEntry = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

internal static class Handshakes {
    public static LinkSession Complete(FakeBrowserPeer peer) {
        using Key cliStatic = Key.Create(KeyAgreementAlgorithm.X25519);
        using CliHandshake handshake = Offered(cliStatic, peer);
        _ = handshake.OnReveal(Parse<HelloSteps.Reveal>(peer.Reveal()));
        return handshake.OnConfirm(Parse<HelloSteps.Confirm>(peer.Confirm()));
    }

    public static CliHandshake Accept(Key cliStatic, FakeBrowserPeer peer) =>
        SessionsEntry.Accept(cliStatic, peer.Sid, Parse<HelloSteps.Commit>(peer.Commit()));

    public static CliHandshake Offered(Key cliStatic, FakeBrowserPeer peer) {
        CliHandshake handshake = Accept(cliStatic, peer);
        peer.ReadOffer(HelloSteps.Encode(handshake.Offer()));
        return handshake;
    }

    public static T Parse<T>(string step) => Assert.IsType<T>(HelloSteps.Parse(step));
}
