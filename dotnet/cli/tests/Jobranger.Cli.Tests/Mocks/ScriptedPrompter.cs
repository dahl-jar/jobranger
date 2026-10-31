using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class ScriptedPrompter(TextWriter output, params object[] answers) : IPrompter {
    public static readonly object Cancel = new();

    private readonly Queue<object> queue = new(answers);

    public Action? OnAsk { get; set; }

    public Action? OnCancel { get; set; }

    public List<IReadOnlyList<PromptChoice>> Choices { get; } = [];

    public List<string> Asked { get; } = [];

    public List<(string Message, string? Default)> TextDefaults { get; } = [];

    public List<(string Message, bool Default)> ConfirmDefaults { get; } = [];

    public Task<string> TextAsync(string message, string? defaultValue, CancellationToken cancellationToken) {
        TextDefaults.Add((message, defaultValue));
        string answer = Next<string>(message, cancellationToken);
        return Task.FromResult(answer.Length == 0 && defaultValue is not null ? defaultValue : answer);
    }

    public Task<string> SecretAsync(string message, CancellationToken cancellationToken) =>
        Task.FromResult(Next<string>(message, cancellationToken));

    public Task<bool> ConfirmAsync(string message, bool defaultValue, CancellationToken cancellationToken) {
        ConfirmDefaults.Add((message, defaultValue));
        return Task.FromResult(Next<bool>(message, cancellationToken));
    }

    public Task<string> SelectAsync(string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken) {
        Choices.Add(choices);
        return Task.FromResult(Next<string>(message, cancellationToken));
    }

    public Task<IReadOnlyList<string>> CheckboxAsync(
        string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken) {
        Choices.Add(choices);
        return Task.FromResult<IReadOnlyList<string>>(Next<string[]>(message, cancellationToken));
    }

    private T Next<T>(string message, CancellationToken cancellationToken) {
        output.WriteLine($"? {message}");
        Asked.Add(message);
        OnAsk?.Invoke();
        Assert.True(queue.Count > 0, $"no answer left for the prompt: {message}");
        object answer = queue.Dequeue();
        if (answer == Cancel) {
            OnCancel?.Invoke();
            throw new OperationCanceledException(cancellationToken);
        }
        return Assert.IsType<T>(answer);
    }
}
