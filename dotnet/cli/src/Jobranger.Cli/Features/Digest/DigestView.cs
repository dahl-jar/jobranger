using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal sealed record DigestView(
    Language Language,
    string Subject,
    string DateLine,
    string Lead,
    IReadOnlyList<SectionView> Sections,
    string HiddenText,
    LinkView? HiddenLink,
    IReadOnlyList<string> Notes,
    IReadOnlyList<LinkView> FooterLinks,
    string StopLine,
    string DroppedNote);
