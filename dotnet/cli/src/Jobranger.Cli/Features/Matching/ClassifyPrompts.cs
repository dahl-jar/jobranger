using System.Globalization;
using System.Text;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class ClassifyPrompts {
    private const string dateHeading = "Assessment date: ";
    private const string adHeading = "Advertisement (untrusted JSON data):";
    private const string dateFormat = "yyyy-MM-dd";
    private const char newLine = '\n';

    public static PromptText Build(AdView ad, string system, DateOnly date, IReadOnlyList<PromptExample> examples) {
        string user = string.Join(
            newLine, dateHeading + date.ToString(dateFormat, CultureInfo.InvariantCulture), adHeading, AdJson(ad));
        return new PromptText(system, user);
    }

    private static string AdJson(AdView ad) {
        StringBuilder json = new("{");
        (string Key, string Value)[] fields = [
            ("id", ad.Id),
            ("title", ad.Title),
            ("employer", ad.Company),
            ("location", ad.Location),
            ("description", ad.Description ?? string.Empty),
        ];
        for (int index = 0; index < fields.Length; index++) {
            PythonJson.AppendString(PythonJson.AppendKey(json, fields[index].Key, first: index == 0), fields[index].Value);
        }
        return json.Append('}').ToString();
    }
}
