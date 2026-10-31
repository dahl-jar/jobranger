using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

internal static class Ipv6Hosts {
    private const int pieceCount = 8;
    private const int minCompressed = 2;
    private const string ipv6Keyword = "ipv6";

    private static readonly SearchValues<char> allowed = SearchValues.Create("0123456789abcdefABCDEF:.");

    public static string Encode(string host) {
        IPAddress address = ParseIpv6(host) ?? throw ContractException.AtRoot(ipv6Keyword);
        byte[] bytes = address.GetAddressBytes();
        int[] pieces = [.. Enumerable.Range(0, pieceCount).Select(index => (int)BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index * sizeof(ushort))))];
        (int start, int length) = LongestZeroRun(pieces);
        if (length < minCompressed) {
            return $"[{Hex(pieces)}]";
        }
        return $"[{Hex(pieces[..start])}::{Hex(pieces[(start + length)..])}]";
    }

    private static IPAddress? ParseIpv6(string host) {
        IPAddress? address = null;
        if (host.AsSpan().ContainsAnyExcept(allowed) || !IPAddress.TryParse(host, out address)) {
            return null;
        }
        return address.AddressFamily == AddressFamily.InterNetworkV6 ? address : null;
    }

    private static (int Start, int Length) LongestZeroRun(int[] pieces) {
        (int Start, int Length) best = (0, 0);
        int index = 0;
        while (index < pieces.Length) {
            int end = index;
            while (end < pieces.Length && pieces[end] == 0) {
                end++;
            }
            if (end - index > best.Length) {
                best = (index, end - index);
            }
            index = end + 1;
        }
        return best;
    }

    private static string Hex(IEnumerable<int> pieces) => string.Join(':', pieces.Select(piece => piece.ToString("x", CultureInfo.InvariantCulture)));
}
