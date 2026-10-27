using System.Reflection;

namespace Jobranger.Cli.Text;

internal static class EmbeddedText {
    public static string Read(string name) {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
