using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class RecordingBatchWriter(int newPerBatch = 0) : IBatchWriter {
    private readonly List<SourceBatch> written = [];

    public string Ids => SourceFixtures.Ids(written.SelectMany(batch => batch.Listings));

    public int Write(SourceBatch batch) {
        written.Add(batch);
        return newPerBatch;
    }
}
