using System.Net;

namespace Jobranger.Cli.Http;

internal static class PublicAddress {
    private const int nat64PrefixBytes = 12;

    private static readonly IPNetwork nat64 = IPNetwork.Parse("64:ff9b::/96");

    private static readonly IPNetwork[] denied = [.. new[] {
        "0.0.0.0/8",
        "10.0.0.0/8",
        "100.64.0.0/10",
        "127.0.0.0/8",
        "169.254.0.0/16",
        "172.16.0.0/12",
        "192.0.0.0/24",
        "192.0.2.0/24",
        "192.88.99.0/24",
        "192.168.0.0/16",
        "198.18.0.0/15",
        "198.51.100.0/24",
        "203.0.113.0/24",
        "224.0.0.0/4",
        "240.0.0.0/4",
        "::/96",
        "64:ff9b:1::/48",
        "100::/64",
        "2001::/23",
        "2001:db8::/32",
        "2002::/16",
        "3fff::/20",
        "fc00::/7",
        "fe80::/10",
        "fec0::/10",
        "ff00::/8",
    }.Select(network => IPNetwork.Parse(network))];

    public static bool IsPublic(IPAddress address) {
        IPAddress plain = Unwrap(address);
        return !denied.Any(network => network.Contains(plain));
    }

    private static IPAddress Unwrap(IPAddress address) {
        if (nat64.Contains(address)) {
            return new IPAddress(address.GetAddressBytes().AsSpan(nat64PrefixBytes));
        }
        return address;
    }
}
