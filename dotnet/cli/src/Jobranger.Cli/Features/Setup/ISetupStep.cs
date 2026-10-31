namespace Jobranger.Cli.Features.Setup;

internal interface ISetupStep {
    string Title { get; }

    Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken);
}
