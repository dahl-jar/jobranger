using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Fields;

public static partial class Urls {
    private const string httpScheme = "http";
    private const string httpsScheme = "https";
    private const string authorityStart = "//";
    private const string schemeEnd = ":";
    private const char portSeparator = ':';
    private const char userinfoEnd = '@';
    private const char bracketOpen = '[';
    private const char bracketClose = ']';
    private const char querySeparator = '?';
    private const char fragmentSeparator = '#';
    private const int firstPrintable = 0x21;
    private const int lastPrintable = 0x7E;
    private const int maxPort = 65535;
    private const string schemeKeyword = "scheme";
    private const string ipv6Keyword = "ipv6";
    private const string userinfoKeyword = "userinfo";
    private const string portKeyword = "port";

    private static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly SearchValues<char> authorityEnd = SearchValues.Create("/?#");
    private static readonly char[] leadingTrim = [.. Enumerable.Range(0, firstPrintable).Select(code => (char)code)];
    private static readonly string[] tabsAndNewlines = ["\t", "\r", "\n"];

    public static string Encode(string url) {
        EnsureUtf8(url);
        string text = tabsAndNewlines.Aggregate(url.TrimStart(leadingTrim), (current, stripped) => current.Replace(stripped, string.Empty, StringComparison.Ordinal));
        string scheme = Scheme(text);
        string rest = text[(scheme.Length + schemeEnd.Length)..];
        if (!rest.StartsWith(authorityStart, StringComparison.Ordinal)) {
            throw ContractException.AtRoot(FailureKeywords.Host);
        }
        rest = rest[authorityStart.Length..];
        int authorityLength = rest.AsSpan().IndexOfAny(authorityEnd);
        string authority = authorityLength < 0 ? rest : rest[..authorityLength];
        string tail = authorityLength < 0 ? string.Empty : rest[authorityLength..];
        string encoded = $"{scheme}{schemeEnd}{authorityStart}{Authority(authority)}{Tail(tail)}";
        if (encoded.Length > ContractLimits.UrlMaxChars) {
            throw ContractException.AtRoot(FailureKeywords.MaxLength);
        }
        return encoded;
    }

    private static void EnsureUtf8(string url) {
        try {
            _ = strictUtf8.GetByteCount(url);
        } catch (EncoderFallbackException) {
            throw ContractException.AtRoot(FailureKeywords.Utf8);
        }
    }

    private static string Scheme(string text) {
        int end = text.IndexOf(schemeEnd, StringComparison.Ordinal);
        string scheme = end < 0 ? string.Empty : text[..end].ToLowerInvariant();
        if (scheme is not (httpScheme or httpsScheme)) {
            throw ContractException.AtRoot(schemeKeyword);
        }
        return scheme;
    }

    private static string Authority(string authority) {
        if (authority.Contains(bracketOpen, StringComparison.Ordinal) != authority.Contains(bracketClose, StringComparison.Ordinal)) {
            throw ContractException.AtRoot(ipv6Keyword);
        }
        int at = authority.LastIndexOf(userinfoEnd);
        string userinfo = at < 0 ? string.Empty : authority[..(at + 1)];
        if (at >= 0 && !Userinfo().IsMatch(authority[..at])) {
            throw ContractException.AtRoot(userinfoKeyword);
        }
        string hostinfo = authority[(at + 1)..];
        bool bracketed = hostinfo.StartsWith(bracketOpen);
        (string host, string? port) = bracketed ? Bracketed(hostinfo) : Plain(hostinfo);
        string encodedHost = bracketed ? Ipv6Hosts.Encode(host) : Host(host);
        string portText = Port(port);
        return $"{userinfo}{encodedHost}{portText}";
    }

    private static (string Host, string? Port) Bracketed(string hostinfo) {
        int close = hostinfo.IndexOf(bracketClose, StringComparison.Ordinal);
        string after = hostinfo[(close + 1)..];
        if (after.Length > 0 && after[0] != portSeparator) {
            throw ContractException.AtRoot(ipv6Keyword);
        }
        return (hostinfo[1..close], after.Length > 0 ? after[1..] : null);
    }

    private static (string Host, string? Port) Plain(string hostinfo) {
        if (hostinfo.Contains(bracketOpen, StringComparison.Ordinal)) {
            throw ContractException.AtRoot(ipv6Keyword);
        }
        int colon = hostinfo.IndexOf(portSeparator, StringComparison.Ordinal);
        return colon < 0 ? (hostinfo, null) : (hostinfo[..colon], hostinfo[(colon + 1)..]);
    }

    private static string Host(string host) {
        string ascii = IdnaHosts.ToAscii(host);
        if (Ipv4Hosts.EndsInNumber(ascii)) {
            return Ipv4Hosts.Encode(ascii);
        }
        if (!DnsHost().IsMatch(ascii)) {
            throw ContractException.AtRoot(FailureKeywords.Host);
        }
        return ascii;
    }

    private static string Port(string? port) {
        if (string.IsNullOrEmpty(port)) {
            return string.Empty;
        }
        int number = int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
        if (number is 0 or > maxPort) {
            throw ContractException.AtRoot(portKeyword);
        }
        return $"{portSeparator}{number.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Tail(string tail) {
        int fragmentStart = tail.IndexOf(fragmentSeparator, StringComparison.Ordinal);
        string fragment = fragmentStart < 0 ? string.Empty : tail[(fragmentStart + 1)..];
        string beforeFragment = fragmentStart < 0 ? tail : tail[..fragmentStart];
        int queryStart = beforeFragment.IndexOf(querySeparator, StringComparison.Ordinal);
        string query = queryStart < 0 ? string.Empty : beforeFragment[(queryStart + 1)..];
        string path = queryStart < 0 ? beforeFragment : beforeFragment[..queryStart];
        StringBuilder encoded = new(Quote(path));
        if (query.Length > 0) {
            _ = encoded.Append(querySeparator).Append(Quote(query));
        }
        if (fragment.Length > 0) {
            _ = encoded.Append(fragmentSeparator).Append(Quote(fragment));
        }
        return encoded.ToString();
    }

    private static string Quote(string part) {
        StringBuilder quoted = new();
        foreach (byte value in Encoding.UTF8.GetBytes(part)) {
            if (value is >= firstPrintable and <= lastPrintable) {
                _ = quoted.Append((char)value);
            } else {
                _ = quoted.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return quoted.ToString();
    }

    [GeneratedRegex(@"^[!-\[\]-~]+\z")]
    private static partial Regex Userinfo();

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)*\.?\z")]
    private static partial Regex DnsHost();
}
