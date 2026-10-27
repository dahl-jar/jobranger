namespace Jobranger.Cli.Features.Digest;

internal sealed record SectionView(string Title, IReadOnlyList<CardView> Cards, int Dropped);
