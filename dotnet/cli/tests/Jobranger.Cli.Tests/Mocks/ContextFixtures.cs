using Jobranger.Cli.Features.Profiles;

namespace Jobranger.Cli.Tests.Mocks;

internal static class ContextFixtures {
    public const string About = "Built a C# CLI.";

    public static readonly DateOnly Today = new(2026, 10, 12);

    public static readonly ContextDoc Doc = new("doc_cv_notes_md", "CV notes.md", "Built a booking API in Kotlin.", "2026-10-12");
}
