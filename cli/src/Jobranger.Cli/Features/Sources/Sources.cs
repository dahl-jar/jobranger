using Jobranger.Cli.Config;
using Jobranger.Cli.Logging;

namespace Jobranger.Cli.Features.Sources;

internal static class Sources {
    public static IReadOnlyList<ISource> Enabled(SourcesConfig sources) => SourceRegistry.Enabled(sources);

    public static async Task<SourceOutcome> FetchAsync(
        ISource source, FetchContext context, CancellationToken cancellationToken) {
        try {
            SourceBatch batch = await source.FetchAsync(context, cancellationToken).ConfigureAwait(false);
            return new SourceOutcome(source.Name, batch, null, cancellationToken.IsCancellationRequested);
        } catch (SourceException error) {
            return new SourceOutcome(source.Name, error.Partial, error.Message, false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return new SourceOutcome(source.Name, null, null, true);
        } catch (Exception error) {
            LogEvents.SourceCrashed(context.Logger, source.Name, error);
            return new SourceOutcome(source.Name, null, error.GetType().Name, false);
        }
    }
}
