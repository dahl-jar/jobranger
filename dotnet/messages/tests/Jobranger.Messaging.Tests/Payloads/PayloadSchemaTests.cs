using Jobranger.Messaging.Payloads;
using Json.Schema;

namespace Jobranger.Messaging.Tests.Payloads;

public sealed class PayloadSchemaTests {
    private const int callerCount = 32;

    [Fact]
    public async Task ShouldBuildSchemaOnce_WhenCallersRace() {
        string[] names = [.. PayloadSchema.DefinitionNodes.Select(entry => entry.Key)];

        List<JsonSchema[]> raced = [];
        foreach (string name in names) {
            raced.Add(await RaceAsync(name));
        }

        Assert.All(raced, schemas => Assert.All(schemas, schema => Assert.Same(schemas[0], schema)));
    }

    private static async Task<JsonSchema[]> RaceAsync(string name) {
        using Barrier start = new(callerCount);
        IEnumerable<Task<JsonSchema>> callers = Enumerable.Range(0, callerCount).Select(_ => Task.Factory.StartNew(
            () => {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                return PayloadSchema.For(name);
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));
        return await Task.WhenAll(callers);
    }
}
