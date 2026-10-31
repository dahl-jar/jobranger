namespace Jobranger.Cli.Features.Digest;

internal static class DigestBuilder {
    private const int applySoonDays = 7;

    private static readonly Comparer<DigestCard> deadlineOrder = Comparer<DigestCard>.Create((left, right) => {
        int undated = (left.Deadline is null).CompareTo(right.Deadline is null);
        return undated != 0 ? undated : string.CompareOrdinal(left.Deadline, right.Deadline);
    });

    public static DigestData Build(DigestInput input, DateOnly today) {
        HashSet<string> carriedIds = new(input.CarriedIds, StringComparer.Ordinal);
        List<DigestCard> wanted = [.. input.Cards.Where(card => !IsExpired(card, today))];
        List<DigestCard> fresh = [.. wanted.Where(card => !carriedIds.Contains(card.Id))];
        return new DigestData {
            Day = today,
            RanAt = input.RanAt,
            ApplySoon = [.. fresh.Where(card => IsDueSoon(card, today)).Order(deadlineOrder)],
            NewToday = [
                .. fresh.Where(card => !IsDueSoon(card, today))
                    .OrderBy(card => !card.IsStrongMatch)
                    .ThenBy(card => card, deadlineOrder),
            ],
            Carried = [.. wanted.Where(card => carriedIds.Contains(card.Id)).Order(deadlineOrder)],
            Hidden = input.Hidden,
            NewAds = input.NewAds,
            SourcesRead = input.SourcesRead,
            EmployerPages = input.SourcesRead.Count(DigestSources.IsDirect),
            FailedSources = input.FailedSources,
            ClosedSinceLast = input.ClosedSinceLast,
            BudgetPaused = input.BudgetPaused,
            Pending = input.Pending,
            MaxYears = input.MaxYears,
            ModelStopped = input.Pending > 0 ? input.ModelStopped : null,
        };
    }

    private static bool IsExpired(DigestCard card, DateOnly today) => card.ApplyExpired || card.DeadlineDate < today;

    private static bool IsDueSoon(DigestCard card, DateOnly today) =>
        card.DeadlineDate is DateOnly deadline && deadline.DayNumber - today.DayNumber <= applySoonDays;
}
