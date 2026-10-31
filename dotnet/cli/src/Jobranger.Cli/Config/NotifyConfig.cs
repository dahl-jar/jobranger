namespace Jobranger.Cli.Config;

internal sealed record NotifyConfig {
    public bool Website { get; init; } = true;

    public EmailConfig Email { get; init; } = new();
}
