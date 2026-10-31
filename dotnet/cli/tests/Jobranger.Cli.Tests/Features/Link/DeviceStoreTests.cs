using System.Buffers.Text;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Link;

[UnsupportedOSPlatform("windows")]
public sealed class DeviceStoreTests : IDisposable {
    private const string otherName = "Safari on iOS";

    private readonly TestDatabase db = new();
    private readonly DeviceStore devices;

    public DeviceStoreTests() {
        devices = new DeviceStore(db.Connection);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldReadBackDevice_WhenDeviceIsAdded() {
        string id = devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        Device stored = Assert.Single(devices.List());
        Assert.Equal(
            (Base64Url.EncodeToString(DeviceFixtures.BrowserKey), Base64Url.EncodeToString(DeviceFixtures.BrowserKey), DeviceFixtures.BrowserName,
                "2026-10-25T09:30:00+00:00", (string?)null),
            (id, stored.Id, stored.Name, stored.PairedAt, stored.LastSeen));
    }

    [Fact]
    public void ShouldStoreRawPublicKey_WhenDeviceIsAdded() {
        devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        Assert.Equal(DeviceFixtures.BrowserKey, (byte[])db.Scalar("SELECT public_key FROM devices")!);
    }

    [Fact]
    public void ShouldKeepOneRow_WhenSameKeyPairsAgain() {
        devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        devices.Add(DeviceFixtures.BrowserKey, otherName, DeviceFixtures.PairedAt.AddDays(1));

        Device stored = Assert.Single(devices.List());
        Assert.Equal((otherName, "2026-10-26T09:30:00+00:00"), (stored.Name, stored.PairedAt));
    }

    [Fact]
    public void ShouldRemoveOnlyThatDevice_WhenRemovingById() {
        string removed = devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);
        string kept = devices.Add(DeviceFixtures.OtherKey, otherName, DeviceFixtures.PairedAt);

        bool found = devices.Remove(removed);

        Assert.Equal((true, kept), (found, Assert.Single(devices.List()).Id));
    }

    [Fact]
    public void ShouldReportMissing_WhenRemovedIdIsUnknown() {
        devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

        Assert.False(devices.Remove(Base64Url.EncodeToString(DeviceFixtures.OtherKey)));
    }

    [Fact]
    public void ShouldRemoveEveryDevice_WhenRemovingAll() {
        devices.Add(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);
        devices.Add(DeviceFixtures.OtherKey, otherName, DeviceFixtures.PairedAt);

        int removed = devices.RemoveAll();

        Assert.Equal((2, 0), (removed, devices.List().Count));
    }
}
