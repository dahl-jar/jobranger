using System.Security.Cryptography;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Tests.Mocks;
using Jobranger.Relay.Core.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests.Tokens;

public sealed class HandleKeysTests {
    private static readonly TimeSpan handleLifetime = TimeSpan.FromMinutes(5);

    [Theory]
    [InlineData(31, 32)]
    [InlineData(32, 31)]
    public void ShouldRejectKeyShorterThan32Bytes(int currentLength, int previousLength) {
        byte[] current = new byte[currentLength];
        byte[] previous = new byte[previousLength];

        Assert.Throws<ArgumentOutOfRangeException>(() => new HandleKeys(current, previous));
    }

    [Fact]
    public void ShouldRejectMissingCurrentKey() {
        Assert.Throws<ArgumentNullException>(() => new HandleKeys(null!, null));
    }

    [Fact]
    public void ShouldKeepSigningKeyWhenCallerClearsItsArray() {
        FakeTimeProvider time = new();
        byte[] current = RandomNumberGenerator.GetBytes(RelayLimits.MinHandleKeyBytes);
        ChannelHandle handles = new(new HandleKeys(current, null), time);
        string handle = handles.SignCli(ChannelIds.One, time.GetUtcNow() + handleLifetime);
        Array.Clear(current);

        HandleClaims? claims = handles.Verify(handle);

        Assert.Equal(new HandleClaims(ChannelIds.One, RelayRole.Cli), claims);
    }
}
