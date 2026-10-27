using System.Text.Json.Nodes;

namespace Jobranger.Cli.Features.Providers;

internal interface IModelClient : IDisposable {
    Task<ModelReply> ClassifyAsync(ClassifyPrompt prompt, JsonObject schema, CancellationToken cancellationToken);

    Task<KeyCheck> CheckKeyAsync(CancellationToken cancellationToken);
}
