using System.Text.Json.Serialization;

namespace Jobranger.Cli.Tests.Mocks;

[JsonSerializable(typeof(string))]
internal sealed partial class GoldenJsonContext : JsonSerializerContext;
