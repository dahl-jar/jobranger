using Jobranger.Cli.Features.Updates;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Updates;

public sealed class InstallMethodsTests : IDisposable {
    private const string cellarBinary = "/opt/homebrew/Cellar/jobranger/0.1.0/bin/jobranger";
    private const string methodVariable = "JOBRANGER_INSTALL_METHOD";
    private const string prefixVariable = "HOMEBREW_PREFIX";
    private const string containerVariable = "DOTNET_RUNNING_IN_CONTAINER";

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Theory]
    [InlineData(cellarBinary, "", "", "Homebrew")]
    [InlineData("/opt/homebrew/bin/jobranger", prefixVariable, "/opt/homebrew", "Homebrew")]
    [InlineData("/opt/homebrew2/bin/jobranger", prefixVariable, "/opt/homebrew", "Archive")]
    [InlineData("/app/jobranger", containerVariable, "true", "Docker")]
    [InlineData("/app/jobranger", containerVariable, "false", "Archive")]
    [InlineData("/home/user/.local/bin/jobranger", "", "", "Archive")]
    [InlineData(cellarBinary, methodVariable, "archive", "Archive")]
    [InlineData(cellarBinary, methodVariable, "pip", "Homebrew")]
    public void ShouldDetectInstallMethod_WhenPathOrEnvironmentMatches(
        string processPath, string name, string value, string expected) {
        Dictionary<string, string> environment = name.Length == 0 ? [] : new() { [name] = value };

        InstallMethod method = InstallMethods.Detect(processPath, environment);

        Assert.Equal(expected, method.ToString());
    }

    [Fact]
    public void ShouldDetectHomebrew_WhenBinaryLinksIntoCellar() {
        string target = Path.Combine(temp.Path, "Cellar", "jobranger", "0.1.0", "bin", "jobranger");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "");
        string link = Path.Combine(Directory.CreateDirectory(temp.File("bin")).FullName, "jobranger");
        File.CreateSymbolicLink(link, target);

        InstallMethod method = InstallMethods.Detect(link, new Dictionary<string, string>());

        Assert.Equal(InstallMethod.Homebrew, method);
    }

    [Fact]
    public void ShouldDetectHomebrew_WhenBinaryLinksIntoCellarThroughAnotherLink() {
        string target = Path.Combine(temp.Path, "Cellar", "jobranger", "0.1.0", "bin", "jobranger");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "");
        string middle = Path.Combine(Directory.CreateDirectory(temp.File("opt")).FullName, "jobranger");
        File.CreateSymbolicLink(middle, target);
        string link = Path.Combine(Directory.CreateDirectory(temp.File("bin")).FullName, "jobranger");
        File.CreateSymbolicLink(link, middle);

        InstallMethod method = InstallMethods.Detect(link, new Dictionary<string, string>());

        Assert.Equal(InstallMethod.Homebrew, method);
    }
}
