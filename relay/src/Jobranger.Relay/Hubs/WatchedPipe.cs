using System.IO.Pipelines;

namespace Jobranger.Relay.Hubs;

internal sealed record WatchedPipe(PipeReader Input, PipeWriter Output) : IDuplexPipe;
