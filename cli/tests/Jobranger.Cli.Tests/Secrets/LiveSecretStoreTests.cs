using System.Collections;
using DBus.Services.Secrets;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

[Collection(LiveSecretStores.Collection)]
public sealed class LiveSecretStoreTests : IAsyncDisposable {
    private const string value = "sk-fake-live-1234";
    private const string otherValue = "sk-fake-live-5678";

    private readonly string service = LiveSecretStores.UniqueService();
    private readonly List<(string Service, string Name)> written = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync() {
        if (!LiveSecretStores.IsLive) {
            return;
        }
        foreach ((string writtenService, string writtenName) in written) {
            ISecretStore store = await LiveSecretStores.OpenAsync(writtenService, CancellationToken.None);
            await store.DeleteAsync(writtenName, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ShouldReadSecret_WhenItWasWritten() {
        ISecretStore store = await OpenAsync();
        await WriteAsync(store, value);

        string? read = await store.ReadAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(value, read);
    }

    [Fact]
    public async Task ShouldReadNewValue_WhenWrittenTwice() {
        ISecretStore store = await OpenAsync();
        await WriteAsync(store, value);
        await WriteAsync(store, otherValue);

        string? read = await store.ReadAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(otherValue, read);
    }

    [Fact]
    public async Task ShouldKeepOneItem_WhenWrittenTwice() {
        Assert.SkipUnless(OperatingSystem.IsLinux() && LiveSecretStores.IsLive, LiveSecretStores.OffReason);
        ISecretStore store = await OpenAsync();
        await WriteAsync(store, value);
        await WriteAsync(store, otherValue);
        Collection collection = await (await SecretService.ConnectAsync(EncryptionType.Dh)).GetDefaultCollectionAsync()
            ?? throw new InvalidOperationException("secret service has no default collection");

        Item[] items = await collection.SearchItemsAsync(
            new Dictionary<string, string> { ["service"] = service, ["username"] = SecretFixtures.SecretName });

        Assert.Single(items);
    }

    [Fact]
    public async Task ShouldReturnNull_WhenSecretWasDeleted() {
        ISecretStore store = await OpenAsync();
        await WriteAsync(store, value);
        await store.DeleteAsync(SecretFixtures.SecretName, Token);

        string? read = await store.ReadAsync(SecretFixtures.SecretName, Token);

        Assert.Null(read);
    }

    [Fact]
    public async Task ShouldTreatMissingSecretAsDeleted_WhenDeleting() {
        ISecretStore store = await OpenAsync();

        Exception? error = await Record.ExceptionAsync(() => store.DeleteAsync(SecretFixtures.SecretName, Token));

        Assert.Null(error);
    }

    [Fact]
    public async Task ShouldReadKey_WhenPythonKeyringWroteIt() {
        Assert.SkipUnless(LiveSecretStores.IsLive, LiveSecretStores.OffReason);
        ISecretStore store = await LiveSecretStores.OpenAsync(LiveSecretStores.PythonService, Token);
        written.Add((LiveSecretStores.PythonService, SecretFixtures.SecretName));

        string? read = await store.ReadAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(LiveSecretStores.PythonValue, read);
    }

    [Fact]
    public async Task ShouldRefuseWithoutPrompt_WhenKeychainIsLocked() {
        Assert.SkipUnless(LiveSecretStores.IsLockedKeychain, LiveSecretStores.LockedOffReason);
        ISecretStore store = await LiveSecretStores.OpenAsync(LiveSecretStores.PythonService, Token);

        SecretStoreUnavailableException refused = await Assert.ThrowsAsync<SecretStoreUnavailableException>(
            () => store.ReadAsync(SecretFixtures.SecretName, Token));

        Assert.EndsWith("error=keychain is locked or needs approval in a terminal", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldOpenOsStore_WhenSecretStoreEnvIsUnset() {
        Assert.SkipUnless(LiveSecretStores.IsLive, LiveSecretStores.OffReason);
        Hashtable environment = new() {
            [SecretServiceStore.BusVariable] = Environment.GetEnvironmentVariable(SecretServiceStore.BusVariable),
        };

        ISecretStore? store = await SecretStores.OpenAsync(environment, canPrompt: false, Token);

        Assert.IsType(OperatingSystem.IsMacOS() ? typeof(KeychainSecretStore) : typeof(SecretServiceStore), store);
    }

    private Task<ISecretStore> OpenAsync() {
        Assert.SkipUnless(LiveSecretStores.IsLive, LiveSecretStores.OffReason);
        return LiveSecretStores.OpenAsync(service, Token);
    }

    private async Task WriteAsync(ISecretStore store, string secret) {
        written.Add((service, SecretFixtures.SecretName));
        await store.WriteAsync(SecretFixtures.SecretName, secret, Token);
    }
}
