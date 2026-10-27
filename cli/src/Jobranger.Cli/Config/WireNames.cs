namespace Jobranger.Cli.Config;

internal static class WireNames {
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();
}
