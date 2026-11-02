namespace Jobranger.Cli.Platform;

internal interface IPrompter {
    Task<string> TextAsync(string message, string? defaultValue, CancellationToken cancellationToken);

    Task<string> SecretAsync(string message, CancellationToken cancellationToken);

    Task<bool> ConfirmAsync(string message, bool defaultValue, CancellationToken cancellationToken);

    Task<string> SelectAsync(string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> CheckboxAsync(
        string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken);
}
