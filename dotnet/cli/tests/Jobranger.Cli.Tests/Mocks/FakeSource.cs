using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeSource(Func<CancellationToken, SourceBatch> fetch) : ISource {
    public string Name => "fake";

    public Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) =>
        Task.FromResult(fetch(cancellationToken));
}
