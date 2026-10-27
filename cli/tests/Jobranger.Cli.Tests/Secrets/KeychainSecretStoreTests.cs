using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStoreTests {
    private const int success = 0;
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

        Assert.EndsWith(StatusError(invalidOwnerEdit), refused.Message, StringComparison.Ordinal);
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

    [Fact]
    public void ShouldTurnInteractionOff_WhenStoreCannotPrompt() {
        bool? interactionAllowed = null;
        KeychainSecretStore store = new(SecretStores.ProductService, canPrompt: false, allowed => {
            interactionAllowed = allowed;
            return success;
        }, () => true);

        store.CheckAccess(SecretFixtures.SecretName);

        Assert.False(interactionAllowed);
    }

    [Fact]
    public void ShouldThrowStatus_WhenInteractionSwitchFails() {
        KeychainSecretStore store = new(SecretStores.ProductService, canPrompt: false, _ => invalidOwnerEdit, () => true);

        SecretStoreUnavailableException refused =
            Assert.Throws<SecretStoreUnavailableException>(() => store.CheckAccess(SecretFixtures.SecretName));

        Assert.EndsWith(StatusError(invalidOwnerEdit), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNameApproval_WhenKeychainIsLocked() {
        KeychainSecretStore store = new(SecretStores.ProductService, canPrompt: false, _ => success, () => false);

        SecretStoreUnavailableException refused =
            Assert.Throws<SecretStoreUnavailableException>(() => store.CheckAccess(SecretFixtures.SecretName));

        Assert.EndsWith(SecretFixtures.ApprovalError, refused.Message, StringComparison.Ordinal);
    }

    private static string StatusError(int status) => "error=status=" + status.ToString(CultureInfo.InvariantCulture);
}
