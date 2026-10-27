using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

public static class Base64Fields {
    private const string base64Keyword = "base64";
    private const string maxBytesKeyword = "maxBytes";

    public static byte[] Decode(string text, int maxBytes) {
        byte[] decoded = [];
        try {
            decoded = Convert.FromBase64String(text);
        } catch (FormatException) {
            throw ContractException.AtRoot(base64Keyword);
        }
        if (Convert.ToBase64String(decoded) != text) {
            throw ContractException.AtRoot(base64Keyword);
        }
        if (decoded.Length > maxBytes) {
            throw ContractException.AtRoot(maxBytesKeyword);
        }
        return decoded;
    }
}
