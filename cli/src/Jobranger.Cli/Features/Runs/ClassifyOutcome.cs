namespace Jobranger.Cli.Features.Runs;

internal sealed record ClassifyOutcome(int Pending, StopReason? Stopped);
