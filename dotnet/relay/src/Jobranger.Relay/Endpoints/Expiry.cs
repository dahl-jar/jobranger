namespace Jobranger.Relay.Endpoints;

internal static class Expiry {
    public static DateTimeOffset After(TimeProvider time, TimeSpan lifetime) => WholeSeconds(time.GetUtcNow() + lifetime);

    public static DateTimeOffset WholeSeconds(DateTimeOffset value) => DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds());
}
