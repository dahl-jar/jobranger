namespace Jobranger.Cli.Platform;

internal interface IChildProcessRunner {
    Task<ChildExit> RunAsync(ChildRequest request, CancellationToken cancellationToken);
}
