using System.Text.RegularExpressions;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Sources;

internal static partial class LinkedInPages {
    private const string employmentTypeName = "Employment type";
    private const int matchTimeoutMs = 1000;

    public static List<Card> ParseSearch(string page) {
        List<Card> cards = [.. CardPattern().Matches(page).Select(match => ToCard(match.Groups[1].Value, match.Groups[2].Value))];
        if (cards.Count == 0 && HtmlText.Strip(page).Length > 0) {
            throw new InvalidDataException("linkedin search page has no job cards");
        }
        return cards;
    }

    public static Posting ParsePosting(string page) {
        Match description = DescriptionPattern().Match(page);
        if (!description.Success) {
            throw new InvalidDataException("linkedin posting has no description");
        }
        Dictionary<string, string> criteria = new(StringComparer.Ordinal);
        foreach (Match match in CriteriaPattern().Matches(page)) {
            criteria[HtmlText.Strip(match.Groups[1].Value)] = HtmlText.Strip(match.Groups[2].Value);
        }
        return new Posting(HtmlText.Strip(description.Groups[1].Value), criteria.GetValueOrDefault(employmentTypeName));
    }

    private static Card ToCard(string jobId, string body) {
        Match date = DatePattern().Match(body);
        return new Card(
            jobId,
            Field(TitlePattern(), body),
            Field(CompanyPattern(), body),
            Field(LocationPattern(), body),
            date.Success ? date.Groups[1].Value : null);
    }

    private static string Field(Regex pattern, string body) =>
        pattern.Match(body) is { Success: true } match ? HtmlText.Strip(match.Groups[1].Value) : string.Empty;

    [GeneratedRegex("data-entity-urn=\"urn:li:jobPosting:([0-9]+)\"(.*?)(?=data-entity-urn=|\\z)",
        RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex CardPattern();

    [GeneratedRegex("base-search-card__title\">(.*?)</h3>", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex TitlePattern();

    [GeneratedRegex("base-search-card__subtitle\">(.*?)</h4>", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex CompanyPattern();

    [GeneratedRegex("job-search-card__location\">(.*?)</span>", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex LocationPattern();

    [GeneratedRegex("<time class=\"job-search-card__listdate[^\"]*\" datetime=\"([^\"]+)\"", RegexOptions.None,
        matchTimeoutMs)]
    private static partial Regex DatePattern();

    [GeneratedRegex("show-more-less-html__markup[^>]*>(.*?)</div>", RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex DescriptionPattern();

    [GeneratedRegex(
        "description__job-criteria-subheader\">(.*?)</h3>\\s*<span class=\"description__job-criteria-text[^\"]*\">(.*?)</span>",
        RegexOptions.Singleline, matchTimeoutMs)]
    private static partial Regex CriteriaPattern();

    internal sealed record Card(string JobId, string Title, string Company, string Location, string? Published);

    internal sealed record Posting(string Description, string? EmploymentType);
}
