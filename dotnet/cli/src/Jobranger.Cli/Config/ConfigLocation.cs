using System.Globalization;

namespace Jobranger.Cli.Config;

internal static class ConfigLocation {
    private const char separator = '.';

    public static string Join(string path, string key) => path.Length == 0 ? key : path + separator + key;

    public static string Item(string path, int index) => Join(path, index.ToString(CultureInfo.InvariantCulture));
}
