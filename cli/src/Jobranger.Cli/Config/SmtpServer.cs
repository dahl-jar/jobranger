using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Jobranger.Cli.Config;

internal sealed record SmtpServer(string Host, int Port, bool ImplicitTls) {
    private const string schemeSeparator = "://";
    private const string plainScheme = "smtp";
    private const string implicitTlsScheme = "smtps";
    private const int plainPort = 587;
    private const int implicitTlsPort = 465;
    private const int minPort = 1;
    private const int maxPort = 65535;
    private const string rootPath = "/";
    private const char portSeparator = ':';
    private const char ipv6Open = '[';
    private const char ipv6Close = ']';
    private const char rootLabel = '.';

    private static readonly SearchValues<char> forbidden = SearchValues.Create("@?#");

    public static SmtpServer? Parse(string url) =>
        ReadScheme(url) is var (implicitTls, afterScheme)
        && Authority(afterScheme) is string authority
        && SplitAuthority(authority) is var (host, portText)
            ? Build(host, portText, implicitTls)
            : null;

    private static SmtpServer? Build(string host, string? portText, bool implicitTls) {
        string? ascii = host.StartsWith(ipv6Open) ? Ipv6Host(host[1..^1]) : AsciiHost(host);
        int? port = portText is null ? DefaultPort(implicitTls) : ReadPort(portText);
        return ascii is null || port is not int known ? null : new SmtpServer(ascii, known, implicitTls);
    }

    private static (bool ImplicitTls, string AfterScheme)? ReadScheme(string url) {
        int schemeEnd = url.IndexOf(schemeSeparator, StringComparison.Ordinal);
        if (schemeEnd < 0) {
            return null;
        }
        string scheme = url[..schemeEnd];
        string rest = url[(schemeEnd + schemeSeparator.Length)..];
        if (string.Equals(scheme, implicitTlsScheme, StringComparison.OrdinalIgnoreCase)) {
            return (true, rest);
        }
        return string.Equals(scheme, plainScheme, StringComparison.OrdinalIgnoreCase) ? (false, rest) : null;
    }

    private static string? Authority(string rest) {
        if (rest.AsSpan().ContainsAny(forbidden)) {
            return null;
        }
        int pathStart = rest.IndexOf(rootPath, StringComparison.Ordinal);
        if (pathStart < 0) {
            return rest;
        }
        return rest[pathStart..] == rootPath ? rest[..pathStart] : null;
    }

    private static (string Host, string? Port)? SplitAuthority(string authority) {
        if (!authority.StartsWith(ipv6Open)) {
            int colon = authority.LastIndexOf(portSeparator);
            return colon < 0 ? (authority, null) : (authority[..colon], authority[(colon + 1)..]);
        }
        int close = authority.IndexOf(ipv6Close, StringComparison.Ordinal);
        string after = authority[(close + 1)..];
        string host = authority[..(close + 1)];
        if (after.Length == 0) {
            return (host, null);
        }
        return after[0] == portSeparator ? (host, after[1..]) : null;
    }

    private static string? Ipv6Host(string host) =>
        IPAddress.TryParse(host, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6
            ? address.ToString()
            : null;

    private static int DefaultPort(bool implicitTls) => implicitTls ? implicitTlsPort : plainPort;

    private static int? ReadPort(string text) {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            || port < minPort
            || port > maxPort) {
            return null;
        }
        return port;
    }

    private static string? AsciiHost(string host) {
        if (host.Contains(portSeparator, StringComparison.Ordinal)) {
            return null;
        }
        string trimmed = host.EndsWith(rootLabel) ? host[..^1] : host;
        return MailAddresses.AsciiDomain(trimmed.ToLowerInvariant());
    }
}
