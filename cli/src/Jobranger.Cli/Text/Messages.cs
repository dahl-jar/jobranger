using System.Collections;
using System.Collections.Frozen;

namespace Jobranger.Cli.Text;

internal static class Messages {
    private const string localeAllVariable = "LC_ALL";
    private const string localeVariable = "LANG";
    private const Language defaultLanguage = Language.En;

    private static readonly char[] localeSeparators = ['_', '-', '.', '@'];
    private static readonly FrozenSet<string> norwegianCodes =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "nb", "no", "nn");
    private static readonly IMessages english = new EnglishMessages();
    private static readonly IMessages norwegian = new NorwegianMessages();

    public static IMessages For(IDictionary environment) {
        string? locale = Read(environment, localeAllVariable) ?? Read(environment, localeVariable);
        Language language = locale is null ? defaultLanguage : LanguageFromLocale(locale);
        return language == Language.Nb ? norwegian : english;
    }

    private static string? Read(IDictionary environment, string name) =>
        environment[name] is string { Length: > 0 } value ? value : null;

    private static Language LanguageFromLocale(string locale) {
        string primary = locale.Split(localeSeparators)[0];
        return norwegianCodes.Contains(primary) ? Language.Nb : defaultLanguage;
    }
}
