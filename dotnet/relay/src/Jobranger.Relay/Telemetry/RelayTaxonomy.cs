using Microsoft.Extensions.Compliance.Classification;

namespace Jobranger.Relay.Telemetry;

internal static class RelayTaxonomy {
    public const string Name = "Relay";

    public static DataClassification Pseudonymous => new(Name, nameof(Pseudonymous));
}
