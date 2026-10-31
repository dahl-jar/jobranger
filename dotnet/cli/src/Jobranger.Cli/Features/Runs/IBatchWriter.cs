using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Features.Runs;

internal interface IBatchWriter {
    int Write(SourceBatch batch);
}
