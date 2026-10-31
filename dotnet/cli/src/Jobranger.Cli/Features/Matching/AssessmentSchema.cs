using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class AssessmentSchema {
    private const string anthropicResource = "Jobranger.Cli.Features.Matching.assessment_anthropic.json";
    private const string openAiResource = "Jobranger.Cli.Features.Matching.assessment_openai.json";

    private static readonly Lazy<string> anthropic = new(() => EmbeddedText.Read(anthropicResource));
    private static readonly Lazy<string> openAi = new(() => EmbeddedText.Read(openAiResource));

    public static string Anthropic => anthropic.Value;

    public static string OpenAI => openAi.Value;
}
