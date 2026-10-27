namespace Jobranger.Cli.Config;

internal sealed record EmailConfig {
    public bool On { get; init; }

    public string? Address { get; init; }

    public EmailVia Via { get; init; } = EmailVia.Smtp;

    public string? SmtpUrl { get; init; }

    public string? SmtpUser { get; init; }

    public string? Sender { get; init; }
}
