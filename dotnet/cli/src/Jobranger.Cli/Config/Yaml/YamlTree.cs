using System.Text.Json.Nodes;

namespace Jobranger.Cli.Config.Yaml;

internal sealed record YamlTree(
    JsonNode? Root,
    IReadOnlyDictionary<string, KeyComments> Comments,
    IReadOnlyDictionary<string, int> KeyOrder,
    IReadOnlyList<string> Trailing,
    IReadOnlyList<ConfigError> Errors,
    IReadOnlyDictionary<string, YamlScalar> Scalars);
