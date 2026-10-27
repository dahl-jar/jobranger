namespace Jobranger.Cli.Features.Sources;

internal interface ISource {
    string Name { get; }

    Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken);
}
