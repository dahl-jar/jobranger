using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FailingBatchWriter : IBatchWriter {
    public int Write(SourceBatch batch) => throw new IOException("database is locked");
}
