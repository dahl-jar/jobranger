namespace Jobranger.Cli.Storage;

internal static class ClassificationReset {
    public const string Columns =
        "label = NULL, summary = NULL, requires_master = 0, is_programming_job = NULL, "
        + "display_title = NULL, role_type = NULL, technologies = NULL, requirements = NULL, label_attempts = 0, "
        + "assessment = NULL, classified_at = NULL";
}
