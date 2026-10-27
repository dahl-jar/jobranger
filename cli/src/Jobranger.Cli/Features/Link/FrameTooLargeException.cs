using Jobranger.Contracts.Limits;

namespace Jobranger.Cli.Features.Link;

internal sealed class FrameTooLargeException(int chars)
    : Exception($"frame is over the relay limit ciphertext_chars={chars} max_chars={ContractLimits.CiphertextB64MaxChars}");
