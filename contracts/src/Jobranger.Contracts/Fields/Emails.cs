using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Fields;

public static class Emails {
    private const string definition = "Email";

    public static bool IsValid(string text) => PayloadValidator.IsValid(definition, text);
}
