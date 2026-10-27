using System.Globalization;

namespace Jobranger.Cli.Config;

internal static class WireNames {
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    public static TEnum Parse<TEnum>(string name) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Where(value => Of(value) == name).Cast<TEnum?>().FirstOrDefault()
        ?? throw new KeyNotFoundException(string.Create(
            CultureInfo.InvariantCulture, $"no {typeof(TEnum).Name} has the wire name {name}"));
}
