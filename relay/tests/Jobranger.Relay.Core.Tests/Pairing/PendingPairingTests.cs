using Jobranger.Relay.Core.Pairing;

namespace Jobranger.Relay.Core.Tests.Pairing;

public sealed class PendingPairingTests {
    [Fact]
    public void ShouldPrintOnlyExpiry() {
        PendingPairing pending = new(new PairingCode("KQFB", "MXTW"), DateTimeOffset.UnixEpoch);

        string printed = pending.ToString();

        Assert.Equal("PendingPairing { ExpiresAt = 1970-01-01T00:00:00.0000000+00:00 }", printed);
    }
}
