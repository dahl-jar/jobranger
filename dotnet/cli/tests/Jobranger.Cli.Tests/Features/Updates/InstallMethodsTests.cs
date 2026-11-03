using Jobranger.Cli.Features.Updates;

namespace Jobranger.Cli.Tests.Features.Updates;

public sealed class InstallMethodsTests {
    private const string methodVariable = "JOBRANGER_INSTALL_METHOD";
    private const string containerVariable = "DOTNET_RUNNING_IN_CONTAINER";
    private const string cliImagePull = "^docker pull ghcr\\.io/[a-z0-9-]+/jobranger/cli$";

    [Theory]
    [InlineData(methodVariable, "docker", "Docker")]
    [InlineData(methodVariable, "archive", "Archive")]
    [InlineData("", "", "Archive")]
    [InlineData(methodVariable, "homebrew", "Archive")]
    [InlineData(containerVariable, "true", "Archive")]
    public void ShouldDetectInstallMethod_WhenEnvironmentMatches(string name, string value, string expected) {
        Dictionary<string, string> environment = name.Length == 0 ? [] : new() { [name] = value };

        InstallMethod method = InstallMethods.Detect(environment);

        Assert.Equal(expected, method.ToString());
    }

    [Fact]
    public void ShouldNameCliImage_WhenInstallIsDocker() {
        string command = InstallMethods.UpgradeCommand(InstallMethod.Docker);

        Assert.Matches(cliImagePull, command);
    }
}
