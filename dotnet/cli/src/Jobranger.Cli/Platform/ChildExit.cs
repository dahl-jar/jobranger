namespace Jobranger.Cli.Platform;

internal sealed record ChildExit(int ExitCode, byte[] Output, bool TimedOut, bool OutputTooLarge);
