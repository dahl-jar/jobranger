using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Tests.Catalogue;

public sealed class CatalogueTests {
    public static TheoryData<string> DeveloperTitles => Titles("developer_titles.txt");

    public static TheoryData<string> OtherTitles => Titles("other_titles.txt");

    [Fact]
    public void ShouldIncludeBaseKeywords_WhenNoRoleIsSelected() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.Contains("Software Engineer", plan.LinkedInKeywords);
    }

    [Fact]
    public void ShouldAddRoleStyrkCode_WhenFrontendIsSelected() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets { Subfields = ["frontend"] });

        Assert.Contains("2513", plan.NavStyrk);
    }

    [Fact]
    public void ShouldListEveryConfigRole_WhenCatalogueLoads() {
        IReadOnlyList<(string Id, string Label)> roles = CatalogueEntry.Load().RoleList();

        Assert.Equal(Targets.AllRoles, [.. roles.Select(role => role.Id)]);
    }

    [Fact]
    public void ShouldLabelRole_WhenRolesAreListed() {
        IReadOnlyList<(string Id, string Label)> roles = CatalogueEntry.Load().RoleList();

        Assert.Equal(("backend", "Backend"), roles[0]);
    }

    [Theory]
    [InlineData("general_software", "Software")]
    [InlineData("backend", "Backend")]
    [InlineData(null, "Other")]
    public void ShouldLabelRole_WhenRoleIdIsLookedUp(string? role, string label) {
        Assert.Equal(label, CatalogueEntry.Load().RoleLabel(role));
    }

    [Theory]
    [InlineData("Backend-utvikler")]
    [InlineData("Systemutvikler")]
    [InlineData("Trainee 2027")]
    public void ShouldMatchTitle_WhenDeveloperWordStartsWord(string title) {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.True(plan.TitleMatches(title));
    }

    [Theory]
    [MemberData(nameof(DeveloperTitles))]
    public void ShouldMatchTitle_WhenTitleIsDeveloperWork(string title) {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.True(plan.TitleMatches(title));
    }

    [Theory]
    [MemberData(nameof(OtherTitles))]
    public void ShouldNotMatchTitle_WhenTitleIsOtherWork(string title) {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.False(plan.TitleMatches(title));
    }

    [Fact]
    public void ShouldMatchTitle_WhenDotStemFollowsLetter() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets { Subfields = ["backend"] });

        Assert.True(plan.TitleMatches("ASP.NET"));
    }

    [Fact]
    public void ShouldNotMatchTitle_WhenStemIsInsideWord() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.False(plan.TitleMatches("B2B-selger av treningsprogrammer"));
    }

    [Fact]
    public void ShouldNotMatchTitle_WhenOnlyGenericWordMatches() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.False(plan.TitleMatches("Konsulent innen regnskap"));
    }

    [Fact]
    public void ShouldMatchTitle_WhenGenericWordHasDeveloperWordBeside() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.True(plan.TitleMatches("Java-konsulent"));
    }

    [Fact]
    public void ShouldMatchAcronym_WhenCaseIsUpper() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.True(plan.TitleMatches("Rådgiver KI"));
    }

    [Fact]
    public void ShouldNotMatchAcronym_WhenCaseIsLower() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.False(plan.TitleMatches("Lag it bra"));
    }

    [Fact]
    public void ShouldMatchRoleWord_WhenRoleIsSelected() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets { Subfields = ["game"] });

        Assert.True(plan.TitleMatches("Unity Gameplay"));
    }

    [Fact]
    public void ShouldSkipSeniorAndLeadTitles_WhenLevelsAreDefault() {
        SourcePlan plan = CatalogueEntry.Load().PlanFor(new Targets());

        Assert.Equal(["lead", "senior"], plan.SkipTitleLevels.Order());
    }

    [Fact]
    public void ShouldSkipOnlyLeadTitles_WhenSeniorLevelIsOn() {
        Targets targets = new() { Limits = new Limits { Levels = ["junior", "senior"] } };

        SourcePlan plan = CatalogueEntry.Load().PlanFor(targets);

        Assert.Equal(["lead"], plan.SkipTitleLevels);
    }

    [Fact]
    public void ShouldListEmployerFeedWithSource_WhenDefaultsAreRead() {
        IReadOnlyList<EmployerFeed> feeds = CatalogueEntry.Load().DefaultEmployerFeeds();

        Assert.Contains("ashby:sanity", feeds.Select(feed => feed.Source));
    }

    private static TheoryData<string> Titles(string name) =>
        [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "Catalogue", name))
            .Where(line => line.Length > 0)];
}
