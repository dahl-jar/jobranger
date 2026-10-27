namespace Jobranger.Cli.Features.Providers;

internal sealed record KeyCheck(bool Ok, string Message) {
    public const string Works = "Key works";
}
