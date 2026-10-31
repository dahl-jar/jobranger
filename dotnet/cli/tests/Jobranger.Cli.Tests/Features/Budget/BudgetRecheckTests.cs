using Jobranger.Cli.Features.Budget;
using static Jobranger.Cli.Tests.Mocks.BudgetGolden;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;

namespace Jobranger.Cli.Tests.Features.Budget;

public sealed class BudgetRecheckTests {
    [Fact]
    public void ShouldEqualGolden_WhenEstimatingRecheck() {
        RecheckEstimate estimate = BudgetEntry.EstimateRecheck(RecheckAds, RecheckSystemChars, RecheckPrices);

        Assert.Equal(new RecheckEstimate(RecheckAds, RecheckUsd), estimate);
    }

    [Fact]
    public void ShouldLeaveCostOut_WhenPriceIsUnknown() {
        RecheckEstimate estimate = BudgetEntry.EstimateRecheck(1, RecheckSystemChars, null);

        Assert.Equal(new RecheckEstimate(1, null), estimate);
    }

    [Fact]
    public void ShouldReturnCount_WhenSystemPromptIsEmpty() {
        RecheckEstimate estimate = BudgetEntry.EstimateRecheck(1, 0, null);

        Assert.Equal(new RecheckEstimate(1, null), estimate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldCostNothing_WhenNoAdIsOutdated(bool priced) {
        ModelPrices? prices = priced ? RecheckPrices : null;

        RecheckEstimate estimate = BudgetEntry.EstimateRecheck(0, RecheckSystemChars, prices);

        Assert.Equal(new RecheckEstimate(0, 0m), estimate);
    }

    [Fact]
    public void ShouldRejectSystemLength_WhenNegative() {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => BudgetEntry.EstimateRecheck(1, -1, RecheckPrices));

        Assert.StartsWith("system_chars must be zero or more, got -1", refused.Message, StringComparison.Ordinal);
    }
}
