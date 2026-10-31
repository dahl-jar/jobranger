using System.Collections.Frozen;

namespace Jobranger.Cli.Config.Yaml;

internal static class YamlNull {
    public const string Text = "null";

    public static readonly FrozenSet<string> Literals =
        FrozenSet.Create(StringComparer.Ordinal, string.Empty, "~", Text, "Null", "NULL");
}
