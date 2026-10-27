using System.Collections;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

public sealed class SecretStoresTests {
    private readonly FakeSecretStore platform = new();
    private int opened;

    [Fact]
    public async Task ShouldNeverOpenPlatformStore_WhenSecretStoreEnvIsFile() {
        Hashtable environment = new() { [SecretStores.SelectVariable] = SecretStores.FileOnly };

        ISecretStore? store = await SecretStores.OpenAsync(environment, OpenPlatform, TestContext.Current.CancellationToken);

        Assert.Equal((null, 0), (store, opened));
    }

    [Fact]
    public async Task ShouldUsePlatformStore_WhenSecretStoreEnvIsUnset() {
        ISecretStore? store = await SecretStores.OpenAsync(new Hashtable(), OpenPlatform, TestContext.Current.CancellationToken);

        Assert.Same(platform, store);
    }

    private Task<ISecretStore?> OpenPlatform(CancellationToken cancellationToken) {
        opened++;
        return Task.FromResult<ISecretStore?>(platform);
    }
}
