using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class MessagesTests {
    [Theory]
    [InlineData(null, Locales.English, typeof(EnglishMessages))]
    [InlineData(null, Locales.Norwegian, typeof(NorwegianMessages))]
    [InlineData(null, "no_NO", typeof(NorwegianMessages))]
    [InlineData(null, "nn_NO.UTF-8@euro", typeof(NorwegianMessages))]
    [InlineData(null, "NB-no", typeof(NorwegianMessages))]
    [InlineData(null, "nb.UTF-8", typeof(NorwegianMessages))]
    [InlineData(null, "nn@euro", typeof(NorwegianMessages))]
    [InlineData("en_GB.UTF-8", Locales.Norwegian, typeof(EnglishMessages))]
    [InlineData("", Locales.Norwegian, typeof(NorwegianMessages))]
    public void ShouldPickLanguage_WhenLcAllOrLangIsSet(string? lcAll, string lang, Type expected) {
        Dictionary<string, string> environment = Locales.EnvironmentWithoutConfig(lang);
        if (lcAll is not null) {
            environment[Locales.LcAllVariable] = lcAll;
        }

        IMessages messages = Messages.For(null, environment);

        Assert.IsType(expected, messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("C")]
    [InlineData("nbx_NO")]
    public void ShouldPickEnglish_WhenLangIsMissingOrUnknown(string? lang) {
        Dictionary<string, string> environment = lang is null ? [] : Locales.EnvironmentWithoutConfig(lang);

        IMessages messages = Messages.For(null, environment);

        Assert.IsType<EnglishMessages>(messages);
    }

    [Theory]
    [InlineData(Locales.English)]
    [InlineData(Locales.Norwegian)]
    public void ShouldNameToken_WhenArgumentIsUnknown(string lang) {
        IMessages messages = Messages.For(null, Locales.EnvironmentWithoutConfig(lang));

        string error = messages.UnknownArgument("extra");

        Assert.Contains("extra", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English)]
    [InlineData(Locales.Norwegian)]
    public void ShouldNameOption_WhenOptionIsMisused(string lang) {
        IMessages messages = Messages.For(null, Locales.EnvironmentWithoutConfig(lang));

        string error = messages.InvalidOption("--count");

        Assert.Contains("--count", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English)]
    [InlineData(Locales.Norwegian)]
    public void ShouldNameProgram_WhenHelpHintIsShown(string lang) {
        IMessages messages = Messages.For(null, Locales.EnvironmentWithoutConfig(lang));

        string hint = messages.HelpHint("jobranger");

        Assert.Contains("jobranger --help", hint, StringComparison.Ordinal);
    }
}
