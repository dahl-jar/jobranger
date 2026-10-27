using System.Net;
using System.Net.Sockets;

namespace Jobranger.Relay.Security;

internal static class ClientPartition {
    private const string unknown = "unknown";
    private const int clientPrefixBits = 64;
    private const int blockPrefixBits = 48;
    private const int ipv4NetworkPrefixBits = 24;
    private const int bitsPerByte = 8;

    public static string For(IPAddress? address) => Unwrapped(address) switch {
        null => unknown,
        { AddressFamily: AddressFamily.InterNetworkV6 } v6 => Prefix(v6, clientPrefixBits),
        { } v4 => v4.ToString(),
    };

    public static string Network(IPAddress? address) => Unwrapped(address) switch {
        null => unknown,
        { AddressFamily: AddressFamily.InterNetworkV6 } v6 => Prefix(v6, blockPrefixBits),
        { } v4 => Prefix(v4, ipv4NetworkPrefixBits),
    };

    public static string? Ipv6Block(IPAddress? address) =>
        Unwrapped(address) is { AddressFamily: AddressFamily.InterNetworkV6 } v6 ? Prefix(v6, blockPrefixBits) : null;

    private static IPAddress? Unwrapped(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;

    private static string Prefix(IPAddress address, int bits) {
        byte[] bytes = address.GetAddressBytes();
        int kept = bits / bitsPerByte;
        Array.Clear(bytes, kept, bytes.Length - kept);
        return $"{new IPAddress(bytes)}/{bits}";
    }
}
