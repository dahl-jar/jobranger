namespace Jobranger.Cli.Features.Delivery;

internal sealed record SendFailure(
    SendFailureKind Kind, string Reason, string ErrorType = nameof(SendFailure), int? Status = null);
