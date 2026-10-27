namespace Jobranger.Relay.Core;

public static class ContractLimits {
    public const int MaxJsonDepth = 64;
    public const int MaxIntegerDigits = 16;
    public const int CiphertextB64MaxChars = 61440;
    public const int GcmTagBytes = 16;
    public const int HelloMaxBytes = 4096;
}
