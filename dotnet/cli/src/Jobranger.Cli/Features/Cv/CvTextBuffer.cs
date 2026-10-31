using System.Text;

namespace Jobranger.Cli.Features.Cv;

internal sealed class CvTextBuffer {
    private readonly StringBuilder parts = new();
    private readonly StringBuilder separators = new();
    private int length;

    public void Separate(char separator) {
        separators.Append(separator);
        if (separators.Length > CvText.MaxChars) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
    }

    public void Append(string text) {
        if (text.Length == 0) {
            return;
        }
        Add(separators.ToString());
        separators.Clear();
        Add(text);
    }

    public string Value() => parts.ToString();

    private void Add(string text) {
        length += text.EnumerateRunes().Count();
        if (length > CvText.MaxChars) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
        parts.Append(text);
    }
}
