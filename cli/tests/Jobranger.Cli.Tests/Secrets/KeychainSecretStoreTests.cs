using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStoreTests {
    private const int itemNotFound = -25300;
    private const int invalidOwnerEdit = -25244;
    private const int interactionNotAllowed = -25308;
    private const int authFailed = -25293;

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
                SecretFixtures.SecretName, invalidOwnerEdit, () => SecretFixtures.FileValue));

        Assert.EndsWith(
            "error=status=" + invalidOwnerEdit.ToString(CultureInfo.InvariantCulture),
            refused.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(interactionNotAllowed)]
    [InlineData(authFailed)]
    public void ShouldNameApproval_WhenKeychainCannotPrompt(int status) {
        SecretStoreUnavailableException refused = Assert.Throws<SecretStoreUnavailableException>(
            () => KeychainSecretStore.ValueForStatus(SecretFixtures.SecretName, status, () => SecretFixtures.FileValue));

        Assert.EndsWith(SecretFixtures.ApprovalError, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldLeaveInteractionSwitchAlone_WhenStoreCanPrompt() {
        KeychainSecretStore store = new(SecretStores.ProductService, canPrompt: true, _ => authFailed, () => false);

        Exception? error = Record.Exception(() => store.CheckAccess(SecretFixtures.SecretName));

        Assert.Null(error);
    }
}
