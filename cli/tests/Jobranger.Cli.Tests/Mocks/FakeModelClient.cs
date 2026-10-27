using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Providers;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeModelClient(Func<ClassifyPrompt, CancellationToken, Task<ModelReply>> answer) : IModelClient {
    private const char lineBreak = '\n';
    private const string idKey = "id";

    private readonly ConcurrentQueue<ClassifyPrompt> prompts = new();

    public IReadOnlyList<ClassifyPrompt> Prompts => [.. prompts];

    public int Calls => prompts.Count;

    public static ModelReply Reply(string text, StopKind stop = StopKind.Done, decimal? cost = 0.01m) =>
        new(text, new TokenUsage(100, 0, 0, 0, 20), stop, cost);

    public static FakeModelClient Keyed(IReadOnlyDictionary<string, string> replies) =>
        new((prompt, _) => Task.FromResult(Reply(replies[AdId(prompt)])));

    public static string AdId(ClassifyPrompt prompt) =>
        JsonNode.Parse(prompt.User.TrimEnd().Split(lineBreak)[^1])![idKey]!.GetValue<string>();

    public Task<ModelReply> ClassifyAsync(ClassifyPrompt prompt, JsonObject schema, CancellationToken cancellationToken) {
        prompts.Enqueue(prompt);
        return answer(prompt, cancellationToken);
    }

    public Task<KeyCheck> CheckKeyAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("fake model client has no key check");

    public void Dispose() {
    }
}
