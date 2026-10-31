namespace Jobranger.Messaging.Settings.Config;

public sealed record ModelConfig(
    string? Provider,
    string? Model,
    string? KeyHint,
    Price Price,
    double? MonthlyLimitUsd);
