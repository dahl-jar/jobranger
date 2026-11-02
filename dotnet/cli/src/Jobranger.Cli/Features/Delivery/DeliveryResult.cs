namespace Jobranger.Cli.Features.Delivery;

internal sealed record DeliveryResult(string? EmailTo = null, string? EmailError = null);
