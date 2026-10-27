using Jobranger.Cli.Features.Ads;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;

namespace Jobranger.Cli.Tests.Features.Ads;

public sealed class DuplicatesParityTests {
    private const int adCount = 500;
    private const int reportedCount = 120;
    private const int seed = 20261025;
    private const int baseTextCount = 24;
    private const int textWordCount = 36;
    private const int shortTextWordCount = 12;
    private const int kindCount = 8;
    private const int maxNearChanges = 4;
    private const string listingUrl = "https://jobs.example.test/ad/";
    private const string trackingQuery = "?utm_source=feed";

    private static readonly string[] vocabulary = [
        "vi", "søker", "utvikler", "team", "kunder", "tjenester", "skyen", "data", "kvalitet", "drift",
        "produkt", "design", "levere", "løsninger", "erfaring", "ansvar", "integrasjoner", "systemer",
        "kontinuerlig", "leveranse", "kodegjennomgang", "testing", "plattform", "sikkerhet", "arkitektur",
        "brukere", "fleksibel", "arbeidstid", "pensjon", "kontor", "bergen", "oslo", "mobil", "apper",
    ];
    private static readonly string[] roleWords = ["android", "ios", "java", "kotlin", "backend", "frontend", "c#"];
    private static readonly string[] titles = [
        "Java-utvikler", "Javautvikler", "Java utvikler", "Kotlinutvikler", "Android Developer", "iOS Developer",
        "Senior Backend Engineer", "Backend Engineer", "Lead Developer", "Utvikler", "UTVIKLER AS",
        "Bli med på laget!", "Vil du bygge apper med oss?", "Fullstack Android Developer",
        "Fullstack iOS Developer", ".NET-utvikler", "C# utvikler", "C++ utvikler", "Graduate developer",
        "Data Engineer", "Konsulent", "Straße-utvikler", "STRASSE utvikler",
    ];
    private static readonly string[] companies = [
        "Org One AS", "Org One", "IT, Org One", "Org One avd Bergen", "Org One Norge", "Org Two AS",
        "Org Two via Agency AS", "Agency AS", "Norway Group AS", "AB", "IT, AB", "Org Three ASA", "",
    ];
    private static readonly string[] locations =
        ["Bergen", "Oslo", "OSLO", "Oslo, Oslo", "", "Bergen, Vestland", "Kongsberg"];
    private static readonly IReadOnlyList<Ad> generated = Generate();

    [Fact]
    public void ShouldGroupLikeReference_WhenAdsAreGenerated() {
        IReadOnlyList<IReadOnlyList<Ad>> expected = ReferenceDuplicates.Group(generated);

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group(generated);

        Assert.Equal(Ids(expected), Ids(groups));
    }

    [Fact]
    public void ShouldSplitLikeReference_WhenAdsAreGenerated() {
        IReadOnlyList<Ad> reported = [.. generated.Take(reportedCount)];
        IReadOnlyList<Ad> fresh = [.. generated.Skip(reportedCount)];
        (IReadOnlyList<Ad> _, IReadOnlyList<string> expected) = ReferenceDuplicates.SplitReported(fresh, reported);

        (IReadOnlyList<Ad> _, IReadOnlyList<string> covered) = Duplicates.SplitReported(fresh, reported);

        Assert.Equal(expected, covered);
    }

    private static List<Ad> Generate() {
        Random random = new(seed);
        string[] baseTexts = [.. Enumerable.Range(0, baseTextCount).Select(_ => Text(random, textWordCount))];
        List<Ad> ads = [];
        for (int index = 0; index < adCount; index++) {
            string id = $"gen:{random.Next(adCount)}-{index}";
            Ad? earlier = ads.Count == 0 ? null : ads[random.Next(ads.Count)];
            Ad ad = (random.Next(kindCount), earlier) switch {
                (0, not null) => earlier with { Url = PlantedUrl(earlier, random, index) },
                (1, not null) => earlier with {
                    Url = PlantedUrl(earlier, random, index),
                    Description = Changed(earlier.Description, random, random.Next(1, maxNearChanges)),
                },
                (2, not null) => earlier with { Title = Pick(random, titles), Url = listingUrl + index },
                (3, not null) => earlier with {
                    Company = Pick(random, companies),
                    Location = Pick(random, locations),
                    Title = random.Next(2) == 0 ? earlier.Title : Pick(random, titles),
                    Url = PlantedUrl(earlier, random, index),
                },
                (4, not null) => earlier with {
                    Description = Changed(earlier.Description, random, random.Next(textWordCount / 2)),
                    Url = listingUrl + index,
                },
                _ => Fresh(random, baseTexts, index),
            };
            ads.Add(ad with { Id = id });
        }
        return ads;
    }

    private static Ad Fresh(Random random, string[] baseTexts, int index) {
        string text = Pick(random, baseTexts);
        string? description = random.Next(6) switch {
            0 => null,
            1 => Text(random, shortTextWordCount),
            2 => text + " " + Pick(random, roleWords),
            3 => Changed(text, random, random.Next(textWordCount)),
            _ => text,
        };
        return Primary with {
            Title = Pick(random, titles),
            Company = Pick(random, companies),
            Location = Pick(random, locations),
            Url = random.Next(10) == 0 ? string.Empty : listingUrl + index,
            Description = description,
        };
    }

    private static string PlantedUrl(Ad earlier, Random random, int index) => random.Next(3) switch {
        0 => earlier.Url,
        1 => earlier.Url + trackingQuery,
        _ => listingUrl + index,
    };

    private static string? Changed(string? text, Random random, int changes) {
        if (text is null) {
            return null;
        }
        string[] words = text.Split(' ');
        for (int change = 0; change < changes; change++) {
            words[random.Next(words.Length)] = Pick(random, vocabulary);
        }
        return string.Join(' ', words);
    }

    private static string Text(Random random, int wordCount) =>
        string.Join(' ', Enumerable.Range(0, wordCount).Select(_ => Pick(random, vocabulary)));

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];

    private static string[][] Ids(IReadOnlyList<IReadOnlyList<Ad>> groups) =>
        [.. groups.Select(group => group.Select(ad => ad.Id).ToArray())];
}
