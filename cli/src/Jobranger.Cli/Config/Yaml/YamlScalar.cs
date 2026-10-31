using YamlDotNet.Core;

namespace Jobranger.Cli.Config.Yaml;

internal sealed record YamlScalar(string Value, ScalarStyle Style, string? Tag);
