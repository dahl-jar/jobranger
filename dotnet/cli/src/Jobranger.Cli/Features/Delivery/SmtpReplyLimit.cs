using MailKit;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class SmtpReplyLimit : IProtocolLogger {
    private const int maxLines = 100;
    private const int maxBytes = 64 * 1024;
    private const int codeLength = 3;
    private const byte continuationMark = (byte)'-';
    private const byte lineFeed = (byte)'\n';

    private int replyBytes;
    private int replyLines;
    private int lineIndex;
    private bool isContinuation;

    public bool IsOver { get; private set; }

    public bool SawNonReply { get; private set; }

    public IAuthenticationSecretDetector? AuthenticationSecretDetector { get; set; }

    public void LogConnect(Uri uri) {
    }

    public void LogClient(byte[] buffer, int offset, int count) {
    }

    public void LogServer(byte[] buffer, int offset, int count) {
        foreach (byte value in buffer.AsSpan(offset, count)) {
            Count(value);
        }
    }

    public void Dispose() {
    }

    private void Count(byte value) {
        replyBytes++;
        if (replyBytes > maxBytes) {
            Refuse();
        }
        if (lineIndex < codeLength && !char.IsAsciiDigit((char)value)) {
            SawNonReply = true;
        }
        if (lineIndex == codeLength && value == continuationMark) {
            isContinuation = true;
        }
        lineIndex++;
        if (value == lineFeed) {
            EndLine();
        }
    }

    private void EndLine() {
        replyLines++;
        if (replyLines > maxLines) {
            Refuse();
        }
        if (!isContinuation) {
            replyLines = 0;
            replyBytes = 0;
        }
        lineIndex = 0;
        isContinuation = false;
    }

    private void Refuse() {
        IsOver = true;
        throw new SmtpReplyTooLongException();
    }
}
