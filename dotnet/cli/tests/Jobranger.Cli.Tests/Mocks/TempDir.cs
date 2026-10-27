namespace Jobranger.Cli.Tests.Mocks;

internal sealed class TempDir : IDisposable {
    private const string prefix = "jobranger-test-";

    public string Path { get; } = Directory.CreateTempSubdirectory(prefix).FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
