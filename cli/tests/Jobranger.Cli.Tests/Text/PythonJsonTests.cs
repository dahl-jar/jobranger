using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class PythonJsonTests {
    [Theory]
    [InlineData(-0.0, "-0.0")]
    [InlineData(2.5, "2.5")]
    [InlineData(0.0, "0.0")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(1e-05, "1e-05")]
    [InlineData(1234567.0, "1234567.0")]
    [InlineData(9999999999999998.0, "9999999999999998.0")]
    [InlineData(1e16, "1e+16")]
    [InlineData(1.2345678901234568e+17, "1.2345678901234568e+17")]
    public void ShouldWriteFloatLikePythonRepr_WhenFormattingNumber(double value, string expected) {
        Assert.Equal(expected, PythonJson.Float(value));
    }
}
