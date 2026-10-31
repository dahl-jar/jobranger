using Microsoft.Extensions.Compliance.Classification;

namespace Jobranger.Relay.Telemetry;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class PseudonymousDataAttribute() : DataClassificationAttribute(RelayTaxonomy.Pseudonymous);
