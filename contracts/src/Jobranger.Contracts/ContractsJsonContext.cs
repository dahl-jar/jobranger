using System.Text.Json.Serialization;
using Jobranger.Contracts.Envelopes;

namespace Jobranger.Contracts;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(Envelope))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
