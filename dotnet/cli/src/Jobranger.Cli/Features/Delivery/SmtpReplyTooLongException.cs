namespace Jobranger.Cli.Features.Delivery;

internal sealed class SmtpReplyTooLongException() : Exception("smtp reply passed the line or byte limit");
