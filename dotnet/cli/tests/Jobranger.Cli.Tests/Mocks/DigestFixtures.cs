using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal static class DigestFixtures {
    public static readonly DateOnly Today = new(2026, 10, 8);

    public static readonly DateTimeOffset RanAt = new(2026, 10, 8, 3, 0, 0, TimeSpan.FromHours(2));

    public static readonly IMessages English = new EnglishMessages();

    public static readonly IMessages Norwegian = new NorwegianMessages();

    public static readonly DigestCard Card = DigestCases.Card;

    public static readonly DigestInput Input = new() {
        Cards = [Card],
        RanAt = RanAt,
        NewAds = 12,
        SourcesRead = ["finn", "nav"],
    };

    public static readonly DigestData Data = new() {
        Day = Today,
        RanAt = RanAt,
        NewToday = [Card],
        NewAds = 12,
        SourcesRead = ["finn", "nav"],
    };

    public static DigestCard Due(string id, string? deadline) => Card with {
        Id = id,
        Deadline = deadline,
        DeadlineDate = Matching.ParseDate(deadline),
    };
}
