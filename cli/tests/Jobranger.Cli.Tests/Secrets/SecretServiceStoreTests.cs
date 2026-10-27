using System.Collections;
using System.Security.Cryptography;
using DBus.Services.Secrets;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Tmds.DBus.Protocol;

namespace Jobranger.Cli.Tests.Secrets;

[Collection(LiveSecretStores.Collection)]
public sealed class SecretServiceStoreTests {
    private const string service = "jobranger-tests";
    private const string lockedError = "org.freedesktop.Secret.Error.IsLocked";

    private static readonly TimeSpan shortTimeout = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan longTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan keyringStartWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(100);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ShouldReturnNoStore_WhenBusAddressIsMissing() {
        SecretServiceStore? store = await SecretServiceStore.OpenAsync(new Hashtable(), service, Token);

        Assert.Null(store);
    }

    [Fact]
    public async Task ShouldReturnNoStore_WhenBusHasNoSecretService() {
        Assert.SkipUnless(LiveSecretStores.IsLiveLinux, LiveSecretStores.LinuxOffReason);
        using PrivateBus bus = new();

        SecretServiceStore? store =
            await SecretServiceStore.OpenAsync(Environment.GetEnvironmentVariables(), service, Token);

        Assert.Null(store);
    }

    [Fact]
    public async Task ShouldReturnNoStore_WhenBusIsUnreachable() {
        Assert.SkipUnless(LiveSecretStores.IsLiveLinux, LiveSecretStores.LinuxOffReason);
        using PrivateBus bus = new();
        bus.StopBus();

        SecretServiceStore? store =
            await SecretServiceStore.OpenAsync(Environment.GetEnvironmentVariables(), service, Token);

        Assert.Null(store);
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenBusDropsAfterConnect() {
        Assert.SkipUnless(LiveSecretStores.IsLiveLinux, LiveSecretStores.LinuxOffReason);
        using PrivateBus bus = new();
        bus.StartKeyring();
        SecretServiceStore store = await ConnectAsync();
        bus.StopBus();

        await Assert.ThrowsAsync<SecretStoreUnavailableException>(
            () => store.ReadAsync(SecretFixtures.SecretName, Token));
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenCallTimesOut() {
        TaskCompletionSource<string?> never = new();

        SecretStoreUnavailableException refused = await Assert.ThrowsAsync<SecretStoreUnavailableException>(
            () => SecretServiceStore.CallAsync(SecretFixtures.SecretName, () => never.Task, shortTimeout, Token));

        Assert.EndsWith("error=timeout", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenServiceRepliesWithError() {
        SecretStoreUnavailableException refused = await Assert.ThrowsAsync<SecretStoreUnavailableException>(
            () => SecretServiceStore.CallAsync(SecretFixtures.SecretName, Locked<string?>, longTimeout, Token));

        Assert.EndsWith("error=" + lockedError, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldThrowUnavailable_WhenDeleteLeavesItems() {
        SecretStoreUnavailableException refused = Assert.Throws<SecretStoreUnavailableException>(
            () => SecretServiceStore.RequireDeleted(SecretFixtures.SecretName, 1));

        Assert.EndsWith("error=delete dismissed", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("error reply")]
    [InlineData("session")]
    public async Task ShouldThrowUnavailable_WhenConnectFails(string failure) {
        Hashtable environment = new() { [SecretServiceStore.BusVariable] = "unix:path=/nonexistent" };
        Func<Task<SecretService>> connect = failure switch {
            "timeout" => () => new TaskCompletionSource<SecretService>().Task,
            "error reply" => Locked<SecretService>,
            _ => () => Task.FromException<SecretService>(new CryptographicException("dh")),
        };

        await Assert.ThrowsAsync<SecretStoreUnavailableException>(
            () => SecretServiceStore.OpenAsync(environment, service, connect, shortTimeout, Token));
    }

    private static Task<T> Locked<T>() => Task.FromException<T>(new DBusErrorReplyException(lockedError, "locked"));

    private static async Task<SecretServiceStore> ConnectAsync() {
        DateTime deadline = DateTime.UtcNow + keyringStartWait;
        while (DateTime.UtcNow < deadline) {
            if (await SecretServiceStore.OpenAsync(Environment.GetEnvironmentVariables(), service, Token) is SecretServiceStore store) {
                return store;
            }
            await Task.Delay(pollInterval, Token);
        }
        throw new TimeoutException("gnome-keyring-daemon did not join the private bus");
    }
}
