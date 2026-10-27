namespace Jobranger.Cli.Features.Runs;

internal static class RunStages {
    private const string sourceStarted = "source_started";
    private const string sourceDone = "source_done";
    private const string sourceFailed = "source_failed";
    private const string model = "model";
    private const string applyChecks = "apply_checks";
    private const string report = "report";

    public static string WireName(RunStage stage) => stage switch {
        RunStage.SourceStarted => sourceStarted,
        RunStage.SourceDone => sourceDone,
        RunStage.SourceFailed => sourceFailed,
        RunStage.Model => model,
        RunStage.ApplyChecks => applyChecks,
        RunStage.Report => report,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "run stage has no wire name"),
    };
}
