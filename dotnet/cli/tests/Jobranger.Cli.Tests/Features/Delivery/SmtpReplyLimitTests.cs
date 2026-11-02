using System.Text;
using Jobranger.Cli.Features.Delivery;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class SmtpReplyLimitTests {
    private const int maxLines = 100;
    private const int maxBytes = 64 * 1024;
    private const string lineEnd = "\r\n";
    private const string continuationLine = "250-x\r\n";
    private const string lastLine = "250 ok\r\n";

    [Theory]
    [InlineData("lines")]
    [InlineData("bytes")]
    public void ShouldAllowReply_WhenAtLimit(string limit) {
        using SmtpReplyLimit reply = new();

        Log(reply, limit == "lines" ? Lines(maxLines) : LineOfBytes(maxBytes));

        Assert.False(reply.IsOver);
    }

    [Theory]
    [InlineData("lines")]
    [InlineData("bytes")]
    [InlineData("no line end")]
    public void ShouldRefuseReply_WhenOverLimit(string limit) {
        using SmtpReplyLimit reply = new();
        string text = limit switch {
            "lines" => Lines(maxLines + 1),
            "bytes" => LineOfBytes(maxBytes + 1),
            _ => new string('2', 200_000),
        };

        Assert.Throws<SmtpReplyTooLongException>(() => Log(reply, text));

        Assert.True(reply.IsOver);
    }

    [Theory]
    [InlineData("lines")]
    [InlineData("bytes")]
    public void ShouldResetCount_WhenReplyEnds(string limit) {
        using SmtpReplyLimit reply = new();
        string text = limit == "lines" ? Lines(60) : LineOfBytes(maxBytes);

        Log(reply, text);
        Log(reply, text);

        Assert.False(reply.IsOver);
    }

    [Fact]
    public void ShouldCountAcrossChunks_WhenReplyArrivesInParts() {
        using SmtpReplyLimit reply = new();
        string text = Lines(maxLines + 1);
        int middle = text.Length / 2 + 1;
        Log(reply, text[..middle]);

        Assert.Throws<SmtpReplyTooLongException>(() => Log(reply, text[middle..]));
    }

    [Theory]
    [InlineData("HTTP/1.1 400 Bad Request\r\n", true)]
    [InlineData("220 ok\r\n", false)]
    [InlineData("\r\n", true)]
    public void ShouldMarkNonReply_WhenLineHasNoCode(string text, bool expected) {
        using SmtpReplyLimit reply = new();

        Log(reply, text);

        Assert.Equal(expected, reply.SawNonReply);
    }

    private static string Lines(int count) => string.Concat(Enumerable.Repeat(continuationLine, count - 1)) + lastLine;

    private static string LineOfBytes(int count) =>
        "250 " + new string('x', count - "250 ".Length - lineEnd.Length) + lineEnd;

    private static void Log(SmtpReplyLimit reply, string text) {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        reply.LogServer(bytes, 0, bytes.Length);
    }
}
