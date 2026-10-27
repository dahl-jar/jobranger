namespace Jobranger.Cli.Config.Yaml;

internal sealed record YamlValue(YamlKind Kind, string Text = "", double Number = 0);
