using Jobranger.Cli.Features.Budget;

namespace Jobranger.Cli.Features.Rechecks;

internal sealed record SaveResult(bool BackToDefault, RecheckEstimate? Estimate);
