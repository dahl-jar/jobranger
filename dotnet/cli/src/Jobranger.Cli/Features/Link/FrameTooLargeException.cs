using Jobranger.Messaging.Limits;

namespace Jobranger.Cli.Features.Link;

internal sealed class FrameTooLargeException(int chars)
    : Exception($"frame is over the relay limit ciphertext_chars={chars} max_chars={MessageLimits.CiphertextB64MaxChars}");
