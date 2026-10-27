using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class TitleLevelsTests {
    [Theory]
    [InlineData("Lead Backend Engineer")]
    [InlineData("Teamleder")]
    [InlineData("Senior Tech Lead")]
    public void ShouldReturnLead_WhenTitleHasLeadWord(string title) {
        Assert.Equal(TitleLevels.Lead, TitleLevels.Of(title));
    }

    [Theory]
    [InlineData("Seniorutvikler")]
    [InlineData("SENIORINGENIØR")]
    [InlineData("Sr. Developer")]
    [InlineData("erfAren utvikler")]
    public void ShouldReturnSenior_WhenTitleHasSeniorWord(string title) {
        Assert.Equal(TitleLevels.Senior, TitleLevels.Of(title));
    }

    [Theory]
    [InlineData("SEN\u0130OR utvikler")]
    [InlineData("\u017Fenior dev")]
    [InlineData("Senior\u0301 dev")]
    public void ShouldReturnSenior_WhenPythonMatchesTitleIgnoringCase(string title) {
        Assert.Equal(TitleLevels.Senior, TitleLevels.Of(title));
    }

    [Theory]
    [InlineData("Experienced trainee")]
    [InlineData("Junior Tech Lead")]
    public void ShouldReturnNoLevel_WhenTitleIsJunior(string title) {
        Assert.Null(TitleLevels.Of(title));
    }

    [Theory]
    [InlineData("Seniority")]
    [InlineData("Senior\u00B2")]
    [InlineData("senior_dev")]
    [InlineData("Srdev")]
    [InlineData("Leadership")]
    public void ShouldReturnNoLevel_WhenLevelWordRunsIntoWord(string title) {
        Assert.Null(TitleLevels.Of(title));
    }
}
