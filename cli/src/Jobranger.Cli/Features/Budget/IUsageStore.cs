namespace Jobranger.Cli.Features.Budget;

internal interface IUsageStore {
    void Add(DateOnly day, UsageCounts counts);

    IReadOnlyList<UsageDay> Days(DateOnly since);

    double SpendBetween(DateOnly start, DateOnly end);
}
