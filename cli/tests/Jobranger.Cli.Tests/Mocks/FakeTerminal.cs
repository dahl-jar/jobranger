namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeTerminal : IDisposable {
    private readonly TaskCompletionSource<string> asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim answered = new();
    private string answer = "";

    public Task<string> AskedAsync() => asked.Task.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

    public string Ask(string prompt) {
        asked.TrySetResult(prompt);
        answered.Wait(TestContext.Current.CancellationToken);
        return answer;
    }

    public void Answer(string text) {
        answer = text;
        answered.Set();
    }

    public void Dispose() {
        answered.Set();
        answered.Dispose();
    }
}
