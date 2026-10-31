using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Tests.Platform;

public sealed class EditorCommandTests {
    [Theory]
    [InlineData("code --wait", "code", new[] { "--wait" })]
    [InlineData("  vim  ", "vim", new string[0])]
    [InlineData("\"/opt/My Editor/edit\" -w", "/opt/My Editor/edit", new[] { "-w" })]
    [InlineData("'/a b/edit' --x=\"y z\"", "/a b/edit", new[] { "--x=y z" })]
    [InlineData("edit \"\"", "edit", new[] { "" })]
    public void ShouldSplitProgramAndArguments_WhenValueHasWords(string value, string program, string[] arguments) {
        EditorCommand? command = EditorCommand.Parse(value);

        Assert.Equal(program, command?.Program);
        Assert.Equal(arguments, command?.Arguments);
    }

    [Fact]
    public void ShouldReturnNull_WhenValueIsBlank() {
        Assert.Null(EditorCommand.Parse("   "));
    }

    [Theory]
    [InlineData("\"/opt/edit")]
    [InlineData("\"\" --wait")]
    public void ShouldThrowNotStarted_WhenValueNamesNoProgram(string value) {
        EditorException error = Assert.Throws<EditorException>(() => EditorCommand.Parse(value));

        Assert.Equal((EditorProblem.NotStarted, value), (error.Problem, error.Program));
    }
}
