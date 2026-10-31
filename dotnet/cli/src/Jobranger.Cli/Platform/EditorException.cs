namespace Jobranger.Cli.Platform;

internal sealed class EditorException(EditorProblem problem, string program, string? editedText = null)
    : Exception($"editor failed problem={problem} program={program}") {
    public EditorProblem Problem { get; } = problem;

    public string Program { get; } = program;

    public string? EditedText { get; } = editedText;
}
