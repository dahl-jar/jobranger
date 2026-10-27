using System.Runtime.Versioning;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStoreTests {
    private const int itemNotFound = -25300;
    private const int interactionNotAllowed = -25244;

    [Fact]
    public void ShouldReturnNoValue_WhenItemIsMissing() {
        string? value =
            KeychainSecretStore.ValueForStatus(SecretFixtures.SecretName, itemNotFound, () => SecretFixtures.FileValue);

        Assert.Null(value);
    }

    [Fact]
    public void ShouldThrowUnavailable_WhenKeychainRefusesRead() {
        SecretStoreUnavailableException refused = Assert.Throws<SecretStoreUnavailableException>(
            () => KeychainSecretStore.ValueForStatus(
                SecretFixtures.SecretName, interactionNotAllowed, () => SecretFixtures.FileValue));

        Assert.EndsWith("error=status=-25244", refused.Message, StringComparison.Ordinal);
    }
}
