using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

public static class Emails {
    private const string definition = "Email";

    public static bool IsValid(string text) => PayloadValidator.IsValid(definition, text);
}
