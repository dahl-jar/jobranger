namespace Jobranger.Cli.Features.Delivery;

internal static class SendLimits {
    public const int DeadlineSeconds = 120;

    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(DeadlineSeconds);
    public static readonly TimeSpan Operation = TimeSpan.FromSeconds(30);
}
