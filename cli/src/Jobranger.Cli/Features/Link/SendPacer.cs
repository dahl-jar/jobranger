namespace Jobranger.Cli.Features.Link;

internal sealed class SendPacer(TimeProvider time, int bytesPerSecond) {
    private const int relayMessagesPerSecond = 20;
    private const int relayMessageBurst = 40;
    private const int messageBurst = relayMessageBurst - relayMessagesPerSecond;

    private static readonly TimeSpan messageInterval = TimeSpan.FromSeconds(1.0 / relayMessagesPerSecond);
    private static readonly TimeSpan burstAllowance = messageInterval * (messageBurst - 1);

    private readonly Lock gate = new();
    private DateTimeOffset bytesFree = DateTimeOffset.MinValue;
    private DateTimeOffset messagesDue = time.GetUtcNow();

    public Task WaitAsync(int bytes, CancellationToken cancellationToken) {
        TimeSpan wait;
        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            DateTimeOffset start = Latest(Latest(now, bytesFree), messagesDue - burstAllowance);
            bytesFree = start + TimeSpan.FromSeconds((double)bytes / bytesPerSecond);
            messagesDue = Latest(messagesDue, start) + messageInterval;
            wait = start - now;
        }
        return wait > TimeSpan.Zero ? Task.Delay(wait, time, cancellationToken) : Task.CompletedTask;
    }

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
