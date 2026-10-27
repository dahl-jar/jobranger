namespace Jobranger.Cli.Features.Cv;

internal sealed class CvUnreadableException : Exception {
    public const string UnsupportedType = "type";
    public const string Unreadable = "unreadable";
    public const string NoText = "no_text";
    public const string TooLarge = "too_large";

    private const string messagePrefix = "cv file unreadable: ";

    public CvUnreadableException(string reason) : base(messagePrefix + reason) {
        Reason = reason;
    }

    public CvUnreadableException(string reason, Exception cause) : base(messagePrefix + reason, cause) {
        Reason = reason;
    }

    public string Reason { get; }
}
