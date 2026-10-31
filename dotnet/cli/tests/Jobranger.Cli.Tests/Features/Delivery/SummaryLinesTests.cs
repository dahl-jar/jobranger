using System.Runtime.Versioning;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Text;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;

namespace Jobranger.Cli.Tests.Features.Delivery;

[UnsupportedOSPlatform("windows")]
public sealed class SummaryLinesTests {
    private const string englishPriceWarning =
        "Your model has no price, so the monthly limit cannot be checked. "
        + "AI checks stay paused until you set a price for the model or remove the limit.";
    private const string norwegianPriceWarning =
        "Modellen din har ingen pris, så månedsgrensen kan ikke sjekkes. "
        + "AI-sjekkene står på pause til du setter en pris på modellen eller fjerner grensen.";
    private const string englishOneDropped =
        "1 ad was dropped because the AI cited something that is not in your profile. Check your guidance.";

    private static readonly RunResult completed = new() { Status = RunStatus.Completed };

    [Theory]
    [InlineData(1, false, englishOneDropped)]
    [InlineData(2, false, "2 ads were dropped because the AI cited something that is not in your profile. Check your guidance.")]
    [InlineData(1, true, "1 annonse ble forkastet fordi AI-en viste til noe som ikke står i profilen din. Sjekk veiledningen din.")]
    [InlineData(2, true, "2 annonser ble forkastet fordi AI-en viste til noe som ikke står i profilen din. Sjekk veiledningen din.")]
    public void ShouldPrintDroppedCount_WhenEvidenceIdsWereUnknown(int dropped, bool norwegian, string line) {
        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(completed with { UnknownEvidenceDropped = dropped }, Messages(norwegian));

        Assert.Equal([line], lines);
    }

    [Theory]
    [InlineData(false, englishPriceWarning)]
    [InlineData(true, norwegianPriceWarning)]
    public void ShouldPrintPriceWarning_WhenLimitHasNoPrice(bool norwegian, string line) {
        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(completed with { UnpricedLimit = true }, Messages(norwegian));

        Assert.Equal([line], lines);
    }

    [Theory]
    [InlineData(false, "AI checks skipped: no model or key. Set one with: jobranger init")]
    [InlineData(true, "AI-sjekkene ble hoppet over: ingen modell eller nøkkel. Sett en med: jobranger init")]
    public void ShouldPrintSkipLine_WhenModelWasSkipped(bool norwegian, string line) {
        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(completed with { ModelSkipped = true }, Messages(norwegian));

        Assert.Equal([line], lines);
    }

    [Fact]
    public void ShouldPutPriceWarningFirst_WhenBothLinesApply() {
        RunResult result = completed with { UnpricedLimit = true, UnknownEvidenceDropped = 1 };

        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(result, new EnglishMessages());

        Assert.Equal([englishPriceWarning, englishOneDropped], lines);
    }

    [Fact]
    public void ShouldPrintNoLine_WhenRunHasNoNotice() {
        IReadOnlyList<string> lines = DeliveryEntry.SummaryLines(completed with { UnknownEvidenceDropped = 0 }, new EnglishMessages());

        Assert.Empty(lines);
    }

    private static IMessages Messages(bool norwegian) => norwegian ? new NorwegianMessages() : new EnglishMessages();
}
