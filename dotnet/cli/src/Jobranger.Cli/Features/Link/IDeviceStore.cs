namespace Jobranger.Cli.Features.Link;

internal interface IDeviceStore {
    string Add(byte[] publicKey, string name, DateTimeOffset pairedAt);

    IReadOnlyList<Device> List();

    bool Remove(string id);

    int RemoveAll();
}
