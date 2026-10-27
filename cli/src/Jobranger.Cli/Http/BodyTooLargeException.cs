namespace Jobranger.Cli.Http;

internal sealed class BodyTooLargeException(long maxBytes)
    : Exception($"response body passed the cap of {maxBytes} bytes");
