using Jobranger.Cli.Features.Ads;

namespace Jobranger.Cli.Tests.Features.Ads;

public sealed class DedupKeyTests {
    [Theory]
    [InlineData("Org One AS", "Utvikler", "org one:utvikler")]
    [InlineData("Org  One\tASA", " Backend   Utvikler ", "org one:backend utvikler")]
    [InlineData("Org One AS ANS", "Utvikler", "org one:utvikler")]
    [InlineData("Org One SF", "Utvikler", "org one:utvikler")]
    [InlineData("AS", "Utvikler", ":utvikler")]
    [InlineData("AS Org One", "Utvikler", "as org one:utvikler")]
    [InlineData("ÆRLIG AS", "Søk", "ærlig:søk")]
    [InlineData("İş AS", "Utvikler", "i\u0307ş:utvikler")]
    [InlineData("Org\u001cOne AS", "Utvikler", "org one:utvikler")]
    [InlineData("Org\u00a0One AS", "Utvikler", "org one:utvikler")]
    [InlineData("ẞ GmbH", "Utvikler", "ß gmbh:utvikler")]
    [InlineData("ΟΔΟΣ AS", "Utvikler", "οδος:utvikler")]
    [InlineData("Σ ΚΑΣΑ", "Utvikler", "σ κασα:utvikler")]
    [InlineData("ΑΣ'Α", "Utvikler", "ασ'α:utvikler")]
    [InlineData("ΆΣ", "Utvikler", "άς:utvikler")]
    [InlineData("ªΣ", "Utvikler", "ªς:utvikler")]
    public void ShouldBuildPythonKey_WhenCompanyAndTitleAreGiven(string company, string title, string expected) {
        Assert.Equal(expected, DedupKey.For(company, title));
    }
}
