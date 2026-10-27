namespace Jobranger.Contracts.Settings;

public sealed record ModelInfo(
    string Id,
    string Name,
    double? InputPerMtok,
    double? OutputPerMtok,
    double? UsdPerDay);
