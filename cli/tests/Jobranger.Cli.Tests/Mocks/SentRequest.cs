using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed record SentRequest(Uri Uri, IReadOnlyDictionary<string, string> Headers, byte[] Body) {
    public string Text => Encoding.UTF8.GetString(Body);

    public string? Header(string name) => Headers.TryGetValue(name, out string? value) ? value : null;
}
