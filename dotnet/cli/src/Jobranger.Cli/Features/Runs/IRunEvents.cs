namespace Jobranger.Cli.Features.Runs;

internal interface IRunEvents {
    void Emit(RunEvent runEvent);
}
