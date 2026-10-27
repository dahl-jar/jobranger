namespace Jobranger.Cli.Storage;

internal interface IStateStore {
    string? Get(string key);

    void Set(string key, string value);

    IReadOnlyDictionary<string, string> Cursors();
}
