using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Platform;

internal sealed class SpectrePrompter : IPrompter {
    private const int pageSize = 10;
    private const string answerGap = "   ";
    private const string labelJoin = ", ";

    private readonly IAnsiConsole console;
    private readonly IMessages messages;

    private SpectrePrompter(IAnsiConsole console, IMessages messages) {
        this.console = console;
        this.messages = messages;
    }

    public static IPrompter? For(IAnsiConsole console, IMessages messages) =>
        console.Profile.Capabilities.Interactive && console.Profile.Capabilities.Ansi
            ? new SpectrePrompter(console, messages)
            : null;

    public async Task<string> TextAsync(string message, string? defaultValue, CancellationToken cancellationToken) {
        string shown = string.IsNullOrEmpty(defaultValue) ? $"{message}:" : $"{message} ({defaultValue}):";
        TextPrompt<string> prompt = new TextPrompt<string>(Markup.Escape(shown)).AllowEmpty();
        string answer = (await ShowAsync(prompt, cancellationToken).ConfigureAwait(false)).Trim();
        return answer.Length == 0 && defaultValue is not null ? defaultValue : answer;
    }

    public async Task<string> SecretAsync(string message, CancellationToken cancellationToken) {
        TextPrompt<string> prompt = new TextPrompt<string>(Markup.Escape($"{message}:")).Secret(null).AllowEmpty();
        return (await ShowAsync(prompt, cancellationToken).ConfigureAwait(false)).Trim();
    }

    public Task<bool> ConfirmAsync(string message, bool defaultValue, CancellationToken cancellationToken) {
        ConfirmationPrompt prompt = new(Markup.Escape(message)) {
            DefaultValue = defaultValue,
            ShowChoices = false,
            ShowDefaultValue = false,
            Yes = messages.PromptYesKey,
            No = messages.PromptNoKey,
            InvalidChoiceMessage = Markup.Escape(messages.PromptYesOrNo),
        };
        return ShowAsync(prompt, cancellationToken);
    }

    public async Task<string> SelectAsync(string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken) {
        SelectionPrompt<PromptChoice> prompt = new SelectionPrompt<PromptChoice>()
            .Title(Markup.Escape(message))
            .PageSize(pageSize)
            .UseConverter(EscapedLabel)
            .MoreChoicesText(Markup.Escape(messages.PromptMoreChoices))
            .AddChoices(choices);
        PromptChoice picked = await ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
        console.WriteLine(message + answerGap + picked.Label);
        return picked.Value;
    }

    public async Task<IReadOnlyList<string>> CheckboxAsync(
        string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken) {
        MultiSelectionPrompt<PromptChoice> prompt = new MultiSelectionPrompt<PromptChoice>()
            .Title(Markup.Escape(message))
            .PageSize(pageSize)
            .NotRequired()
            .UseConverter(EscapedLabel)
            .InstructionsText(Markup.Escape(messages.PromptCheckboxHelp))
            .MoreChoicesText(Markup.Escape(messages.PromptMoreChoices))
            .AddChoices(choices);
        foreach (PromptChoice choice in choices.Where(choice => choice.Checked)) {
            prompt.Select(choice);
        }
        List<PromptChoice> picked = await ShowAsync(prompt, cancellationToken).ConfigureAwait(false);
        console.WriteLine(message + answerGap + string.Join(labelJoin, picked.Select(choice => choice.Label)));
        return [.. picked.Select(choice => choice.Value)];
    }

    private static string EscapedLabel(PromptChoice choice) => Markup.Escape(choice.Label);

    private async Task<T> ShowAsync<T>(IPrompt<T> prompt, CancellationToken cancellationToken) {
        try {
            return await prompt.ShowAsync(console, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            console.Cursor.Show();
            console.WriteLine();
            throw;
        }
    }
}
