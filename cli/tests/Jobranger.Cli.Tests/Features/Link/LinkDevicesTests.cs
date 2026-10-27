using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using LinkEntry = Jobranger.Cli.Features.Link.Link;

namespace Jobranger.Cli.Tests.Features.Link;

[UnsupportedOSPlatform("windows")]
public sealed class LinkDevicesTests : IAsyncDisposable {
    private readonly TempDir temp = new();
    private readonly TestDatabase db = new();
    private readonly FakeRelay relay = new(new FakeTimeProvider(DeviceFixtures.PairedAt));
    private readonly HttpClient http;
    private readonly LinkEntry link;

    public LinkDevicesTests() {
        http = relay.Client();
        Credentials credentials = new(
            new Hashtable(), null, new FileSecretStore(temp.File(SecretFixtures.FileName), NullLogger.Instance));
        LinkParts parts = new(new DeviceStore(db.Connection), new RelayApiClient(http, FakeRelay.Address), relay);
        link = new LinkEntry(credentials, temp.Path, NullLogger.Instance, parts);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync() {
        await link.DisposeAsync();
        http.Dispose();
        relay.Dispose();
        db.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldRefuseNewKeys_WhenDevicesExistWithoutKeys() {
        link.AddDevice(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => link.LoadOrCreateKeysAsync(Token));

        Assert.Contains("jobranger unpair", refused.Message);
    }

    [Fact]
    public void ShouldForgetOneDevice_WhenRemovingById() {
        string removed = link.AddDevice(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);
        string kept = link.AddDevice(DeviceFixtures.OtherKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        link.RemoveDevice(removed);

        Assert.Equal(kept, Assert.Single(link.Devices()).Id);
    }

    [Fact]
    public void ShouldForgetEveryDevice_WhenRemovingAll() {
        link.AddDevice(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        link.RemoveAllDevices();

        Assert.Empty(link.Devices());
    }
}
