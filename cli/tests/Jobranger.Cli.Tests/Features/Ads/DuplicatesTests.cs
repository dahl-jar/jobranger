using Jobranger.Cli.Features.Ads;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;

namespace Jobranger.Cli.Tests.Features.Ads;

public sealed class DuplicatesTests {
    private const string text =
        "vi søker en utvikler som vil bygge tjenester for kundene våre i et team med "
        + "erfarne kolleger der du får ansvar for backend og integrasjoner mot andre "
        + "systemer og jobber tett med produkt og design for å levere løsninger som "
        + "brukes av mange hver dag og du lærer mye om skyplattform drift og kvalitet "
        + "gjennom kodegjennomgang testing og kontinuerlig leveranse til produksjon";
    private const string otherText =
        "org one er et selskap i vekst med kontorer i bergen og oslo vi tilbyr fleksibel arbeidstid gode "
        + "pensjonsordninger og et faglig sterkt miljø med mye kunnskapsdeling på tvers av teamene";
    private const string shortText = "Bygg tjenester i Java og Kotlin for kundene våre.";
    private const string listingUrl = "https://jobs.example.test/ad/";
    private const int textWords = 62;
    private const string longWords =
        "kodegjennomgang kontinuerlig leveranse produksjonsmiljø integrasjonsplattform skyplattformer "
        + "kvalitetssikring systemarkitektur tjenesteutvikling brukeropplevelse driftsovervåking "
        + "sikkerhetsarbeid automatisering dokumentasjon";

    private static readonly string nearCopy = Replaced(textWords / 2, 1);
    private static readonly string looseCopy = Replaced(textWords - 4, 4);
    private static readonly Ad osloListing = Listing("finn:1") with { Location = "Oslo" };
    private static readonly Ad departmentTwin =
        Listing("nav:1") with { Company = "IT, Org One", Location = "Oslo, Oslo" };

    [Fact]
    public void ShouldMatchVacancy_WhenCompanyIsSubsetAndTitleIsSame() {
        Assert.True(Duplicates.IsSameVacancy(osloListing, departmentTwin));
    }

    [Theory]
    [InlineData("Org One AS")]
    [InlineData("Org One Norge")]
    public void ShouldKeepAdsApart_WhenSameTitleHasUnrelatedDescription(string rightCompany) {
        Ad left = Listing("finn:1") with { Description = text };
        Ad right = Listing("finn:2") with { Company = rightCompany, Description = otherText };

        Assert.False(Duplicates.IsSameVacancy(left, right));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenSameTitleHasRewordedDescription() {
        Ad finn = Listing("finn:1") with { Description = text };
        Ad nav = Listing("nav:1") with { Description = looseCopy };

        Assert.True(Duplicates.IsSameVacancy(finn, nav));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenCompanyHasOnlyNoiseWords() {
        Ad finn = Listing("finn:1") with { Company = "Norway Group AS" };
        Ad nav = Listing("nav:1") with { Company = "Norway Group AS" };

        Assert.True(Duplicates.IsSameVacancy(finn, nav));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenDescriptionsOverlapPastThreshold() {
        string[] words = text.Split(' ');
        words[^1] = "drift";
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text };
        Ad linkedIn = Listing("linkedin:1") with {
            Title = "Turn code into impact",
            Description = string.Join(' ', words),
        };

        Assert.True(Duplicates.IsSameVacancy(finn, linkedIn));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenDescriptionsOverlapUnderThreshold() {
        string[] words = longWords.Split(' ')[..12];
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = string.Join(' ', words) };
        words[^1] = "plattform";
        Ad linkedIn = Listing("linkedin:1") with {
            Title = "Turn code into impact",
            Description = string.Join(' ', words),
        };

        Assert.False(Duplicates.IsSameVacancy(finn, linkedIn));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenDescriptionOverlapIsAtThreshold() {
        string[] words = longWords.Split(' ');
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = longWords };
        Ad linkedIn = Listing("linkedin:1") with {
            Title = "Turn code into impact",
            Description = string.Join(' ', words[..^1]),
        };

        Assert.True(Duplicates.IsSameVacancy(finn, linkedIn));
    }

    [Fact]
    public void ShouldGroupAds_WhenDescriptionIsNearCopy() {
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text };
        Ad linkedIn = Listing("linkedin:1") with { Title = "Turn code into impact", Description = nearCopy };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([finn, linkedIn]);

        Assert.Equal([["finn:1", "linkedin:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenDescriptionRatioIsUnderThreshold() {
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text };
        Ad linkedIn = Listing("linkedin:1") with { Title = "Turn code into impact", Description = looseCopy };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([finn, linkedIn]);

        Assert.Equal([["finn:1"], ["linkedin:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepNearCopiesApart_WhenLocationsDiffer() {
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text };
        Ad linkedIn = Listing("linkedin:1") with {
            Title = "Turn code into impact",
            Location = "Bergen, Vestland",
            Description = nearCopy,
        };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([finn, linkedIn]);

        Assert.Equal([["finn:1"], ["linkedin:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepNearCopiesApart_WhenCompaniesDiffer() {
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text };
        Ad linkedIn = Listing("linkedin:1") with {
            Title = "Turn code into impact",
            Company = "Org Two AS",
            Description = nearCopy,
        };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([finn, linkedIn]);

        Assert.Equal([["finn:1"], ["linkedin:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepNearCopiesApart_WhenRolesDiffer() {
        Ad android = Listing("finn:1") with { Title = "Android Developer", Description = text };
        Ad ios = Listing("finn:2") with { Title = "iOS Developer", Description = nearCopy };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([android, ios]);

        Assert.Equal([["finn:1"], ["finn:2"]], Ids(groups));
    }

    [Theory]
    [InlineData("Fullstack Android Developer", "Fullstack iOS Developer")]
    [InlineData("Java/Kotlin-utvikler", ".NET-utvikler")]
    [InlineData("Android-utvikler", "iOS-utvikler")]
    public void ShouldKeepRolesApart_WhenOnlyPlatformDiffers(string leftTitle, string rightTitle) {
        Ad left = Listing("finn:1") with { Title = leftTitle, Description = text };
        Ad right = Listing("finn:2") with { Title = rightTitle, Description = text };

        Assert.False(Duplicates.IsSameVacancy(left, right));
    }

    [Theory]
    [InlineData("Frontendutvikler", "Backendutvikler")]
    [InlineData("Javautvikler", "Kotlinutvikler")]
    [InlineData("Androidutvikler", "Iosutvikler")]
    [InlineData("Javascriptutvikler", "Javautvikler")]
    public void ShouldKeepRolesApart_WhenCompoundTitlesNameOtherRoles(string leftTitle, string rightTitle) {
        Ad left = Listing("finn:1") with { Title = leftTitle, Description = text };
        Ad right = Listing("finn:2") with { Title = rightTitle, Description = text };

        Assert.False(Duplicates.IsSameVacancy(left, right));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenCompoundTitleNamesSameRole() {
        Ad left = Listing("finn:1") with { Title = "Javautvikler", Description = text };
        Ad right = Listing("nav:1") with { Title = "Java-utvikler", Description = text };

        Assert.True(Duplicates.IsSameVacancy(left, right));
    }

    [Fact]
    public void ShouldKeepRolesApart_WhenSloganTitlesHaveOtherPlatformDescriptions() {
        Ad android = Listing("finn:1") with { Title = "Bli med på laget!", Description = text + " android" };
        Ad ios = Listing("finn:2") with { Title = "Vil du bygge apper med oss?", Description = text + " ios" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([android, ios]);

        Assert.Equal([["finn:1"], ["finn:2"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepRolesApart_WhenTitleLevelDiffers() {
        Ad left = Listing("finn:1") with { Title = "Fullstack Engineer", Description = text };
        Ad right = Listing("finn:2") with { Title = "Senior Fullstack Engineer", Description = text };

        Assert.False(Duplicates.IsSameVacancy(left, right));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenSameTitleIsInOtherCity() {
        Ad bergen = Listing("finn:1");
        Ad kongsberg = Listing("finn:2") with { Location = "Kongsberg" };

        Assert.False(Duplicates.IsSameVacancy(bergen, kongsberg));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenOneLocationIsEmpty() {
        Ad left = Listing("finn:1") with { Location = "" };

        Assert.True(Duplicates.IsSameVacancy(left, departmentTwin));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenSharedDescriptionIsShort() {
        Ad left = Listing("finn:1") with { Title = "Utvikler", Description = shortText };
        Ad right = Listing("nav:1") with { Title = "Konsulent", Description = shortText };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([left, right]);

        Assert.Equal([["finn:1"], ["nav:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldMatchVacancy_WhenSharedDescriptionHasMinLength() {
        Ad finn = Listing("finn:1") with { Title = "Graduate developer", Description = text[..120] };
        Ad linkedIn = Listing("linkedin:1") with { Title = "Turn code into impact", Description = text[..120] };

        Assert.True(Duplicates.IsSameVacancy(finn, linkedIn));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldKeepAdsApart_WhenOneDescriptionIsShort(bool shortFirst) {
        Ad complete = Listing("finn:1") with { Title = "Utvikler", Description = text[..130] };
        Ad cut = Listing("nav:1") with { Title = "Konsulent", Description = text[..119] };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group(shortFirst ? [cut, complete] : [complete, cut]);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenCompanyDiffers() {
        Ad left = Listing("finn:1") with { Description = text };
        Ad right = Listing("finn:2") with { Company = "Org Two AS", Description = text };

        Assert.False(Duplicates.IsSameVacancy(left, right));
    }

    [Theory]
    [InlineData("Stra\u00DFe-utvikler", "STRASSE utvikler")]
    [InlineData("Cafe\u0301 utvikler", "Cafe utvikler")]
    public void ShouldMatchVacancy_WhenTitlesMatchAfterPythonNormalising(string leftTitle, string rightTitle) {
        Ad left = Listing("finn:1") with { Title = leftTitle };
        Ad right = Listing("nav:1") with { Title = rightTitle };

        Assert.True(Duplicates.IsSameVacancy(left, right));
    }

    [Theory]
    [InlineData("Virksomhetsteknologi, Org One", "Org One")]
    [InlineData("Org One Services", "IT, Org One")]
    [InlineData("Org One Norge", "Org One")]
    [InlineData("Org One avd Bergen", "Org One avd Oslo")]
    public void ShouldMatchCompany_WhenTokensAreSubset(string left, string right) {
        Assert.True(Duplicates.CompaniesMatch(left, right));
    }

    [Fact]
    public void ShouldNotMatchCompany_WhenOnlyNoiseWords() {
        Assert.False(Duplicates.CompaniesMatch("AS", "AS"));
    }

    [Fact]
    public void ShouldNotMatchCompany_WhenTokensAreNotSubset() {
        Assert.False(Duplicates.CompaniesMatch("Agency AS", "Org One via Agency AS"));
    }

    [Fact]
    public void ShouldNotMatchCompany_WhenSharedTokensAreShort() {
        Assert.False(Duplicates.CompaniesMatch("IT, AB", "AB"));
    }

    [Fact]
    public void ShouldGroupTwins_WhenOneHasDepartmentPrefix() {
        Ad other = Listing("finn:2") with { Company = "Org Two AS" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([osloListing, other, departmentTwin]);

        Assert.Equal([["finn:1", "nav:1"], ["finn:2"]], Ids(groups));
    }

    [Theory]
    [InlineData("finn:1", "finn:2", "finn:3")]
    [InlineData("finn:2", "finn:3", "finn:1")]
    public void ShouldGroupAdsTheSame_WhenInputOrderChanges(string first, string second, string third) {
        Dictionary<string, Ad> ads = new() {
            ["finn:1"] = Listing("finn:1") with { Location = "" },
            ["finn:2"] = Listing("finn:2") with { Location = "Oslo" },
            ["finn:3"] = Listing("finn:3"),
        };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([ads[first], ads[second], ads[third]]);

        Assert.Equal([["finn:1", "finn:2", "finn:3"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenTitlesAreOnlySimilar() {
        Ad left = Listing("finn:1") with { Title = "Backend Utvikler" };
        Ad right = Listing("finn:2") with { Title = "Backend Utviklere" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([left, right]);

        Assert.Equal([["finn:1"], ["finn:2"]], Ids(groups));
    }

    [Theory]
    [InlineData("HTTPS://WWW.Example.TEST/job/1/?utm_source=feed&utm_medium=x#a")]
    [InlineData(" https://www.example.test/job/1")]
    public void ShouldGroupAds_WhenUrlMatchesAfterCanonicalForm(string url) {
        Ad left = Listing("finn:1") with { Url = url };
        Ad right = Listing("nav:1") with { Company = "Org Two AS", Url = "https://www.example.test/job/1" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([left, right]);

        Assert.Equal([["finn:1", "nav:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenBothUrlsAreEmpty() {
        Ad left = Listing("finn:1") with { Url = "" };
        Ad right = Listing("nav:1") with { Company = "Org Two AS", Url = "" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([left, right]);

        Assert.Equal([["finn:1"], ["nav:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldKeepAdsApart_WhenUrlsDifferOnlyInQuery() {
        Ad left = Listing("finn:1") with { Url = "https://ats.example.test/apply?id=1&utm_source=feed" };
        Ad right = Listing("nav:1") with { Company = "Org Two AS", Url = "https://ats.example.test/apply?id=2" };

        IReadOnlyList<IReadOnlyList<Ad>> groups = Duplicates.Group([left, right]);

        Assert.Equal([["finn:1"], ["nav:1"]], Ids(groups));
    }

    [Fact]
    public void ShouldSplitOutTwins_WhenVacancyWasReported() {
        Ad reported = Listing("finn:1");
        Ad twin = departmentTwin with { Location = "Bergen" };
        Ad fresh = Listing("nav:10") with { Company = "Org Two AS" };

        (IReadOnlyList<Ad> remaining, IReadOnlyList<string> covered) =
            Duplicates.SplitReported([twin, fresh], [reported, Listing("finn:7") with { Company = "Org Three AS" }]);

        Assert.Equal(["nav:10"], remaining.Select(ad => ad.Id));
        Assert.Equal(["nav:1"], covered);
    }

    private static string Replaced(int start, int count) {
        string[] words = text.Split(' ');
        Array.Fill(words, "plattform", start, count);
        return string.Join(' ', words);
    }

    private static Ad Listing(string id) => Primary with { Id = id, Url = listingUrl + id };

    private static string[][] Ids(IReadOnlyList<IReadOnlyList<Ad>> groups) =>
        [.. groups.Select(group => group.Select(ad => ad.Id).ToArray())];
}
