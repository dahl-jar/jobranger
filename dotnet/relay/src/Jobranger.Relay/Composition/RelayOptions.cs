using Jobranger.Relay.Core.Tokens;

namespace Jobranger.Relay.Composition;

public sealed class RelayOptions {
    public const string Section = "Relay";

    public string SiteOrigin { get; set; } = string.Empty;

    public string HandleKey { get; set; } = string.Empty;

    public string? PreviousHandleKey { get; set; }

    public string RedactionKey { get; set; } = string.Empty;

    public IReadOnlyList<string> KnownProxyNetworks { get; set; } = [];

    public HandleKeys ToHandleKeys() => new(
        Convert.FromBase64String(HandleKey),
        string.IsNullOrEmpty(PreviousHandleKey) ? null : Convert.FromBase64String(PreviousHandleKey));
}
