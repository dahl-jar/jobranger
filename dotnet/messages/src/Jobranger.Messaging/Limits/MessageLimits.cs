namespace Jobranger.Messaging.Limits;

public static class MessageLimits {
    public const int FrameMaxBytes = 65536;
    public const int CiphertextB64MaxChars = 61440;
    public const int GcmTagBytes = 16;
    public const int ChunkThresholdBytes = 45056;
    public const int StepTimeoutSeconds = 15;
    public const int PairConfirmTimeoutSeconds = 120;
    public const int HelloMaxBytes = 4096;
    public const int AgentMaxBytes = 256;
    public const string PairTagLabel = "jobranger pair v1";
    public const int PairTagBytes = 16;
    public const int AgentDisplayMaxChars = 48;
    public const int ChunkDataMaxBytes = 32768;
    public const int ChunkMaxCount = 256;
    public const int CvMaxBytes = 5242880;
    public const int UrlMaxChars = 2048;
    public const int ReassemblyTimeoutSeconds = 60;
    public const int ReassemblyMaxOpen = 2;
    public const int CommandDedupeSeconds = 600;
    public const int ContractVersion = 1;
    public const int MaxJsonDepth = 64;
    public const int MaxIntegerDigits = 16;
    public const int GuidanceLimit = 12000;
    public const int AboutLimit = 4000;
    public const int DocsLimit = 10;
}
