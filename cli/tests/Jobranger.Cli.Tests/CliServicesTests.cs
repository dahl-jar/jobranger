using System.Runtime.Versioning;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests;

public sealed class CliServicesTests : IDisposable {
    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void ShouldUseConfigLanguage_WhenConfigSetsIt() {
        File.WriteAllText(temp.File("config.yaml"), "language: nb\n");

        CliServices services = Create(Locales.English);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldUseLocaleLanguage_WhenConfigIsMissing() {
        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldUseLocaleLanguage_WhenConfigIsNotYaml() {
        File.WriteAllText(temp.File("config.yaml"), "language: en\nname: a: b\n");

        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ShouldUseLocaleLanguage_WhenConfigIsUnreadable() {
        string path = temp.File("config.yaml");
        File.WriteAllText(path, "language: en\n");
        File.SetUnixFileMode(path, UnixFileMode.None);

        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    private CliServices Create(string lang) => CliServices.Create(
        console.Console,
        console.Console,
        new Dictionary<string, string> { [Locales.LangVariable] = lang, [Locales.ConfigDirVariable] = temp.Path });
}
