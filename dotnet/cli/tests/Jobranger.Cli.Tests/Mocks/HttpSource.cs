using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class HttpSource(string name, Uri url) : ISource {
    public string Name => name;

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        await SourceHttp.GetAsync(context.Http, url, new SourceRequestOptions(), cancellationToken).ConfigureAwait(false);
        return new SourceBatch { Source = name };
    }
}
