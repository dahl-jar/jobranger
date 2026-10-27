using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal static class Utf8Fixtures {
    private const byte invalidByte = 0xFF;

    public static byte[] WithInvalidByte(string before, string after) =>
        [.. Encoding.UTF8.GetBytes(before), invalidByte, .. Encoding.UTF8.GetBytes(after)];
}
