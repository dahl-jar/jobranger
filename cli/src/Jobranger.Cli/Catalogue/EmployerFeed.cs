namespace Jobranger.Cli.Catalogue;

internal sealed record EmployerFeed(string Kind, string Key, string Company, string Board) {
    public static readonly IReadOnlyList<string> AllKinds = ["ashby", "teamtailor"];
}
