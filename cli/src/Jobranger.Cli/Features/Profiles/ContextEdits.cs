using Jobranger.Cli.Text;
using Jobranger.Contracts.Limits;

namespace Jobranger.Cli.Features.Profiles;

internal static class ContextEdits {
    public const int AboutLimit = ContractLimits.AboutLimit;
    public const int DocTextLimit = 12_000;
    public const int DocsLimit = ContractLimits.DocsLimit;
    public const int TextLimit = 40_000;

    private const string aboutId = "about_me";

    public static UserContext WithAbout(UserContext context, string text) {
        int length = CodePoints.Count(text);
        if (length > AboutLimit) {
            throw new ContextFullException($"about text is {length} characters, over {AboutLimit}");
        }
        EnsureRoom(context.TextLength - CodePoints.Count(context.About) + length);
        return context with { About = text };
    }

    public static UserContext WithDoc(UserContext context, string name, string text, DateOnly today) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ContextDocNameException("document name is empty");
        }
        if (context.Docs.Count >= DocsLimit) {
            throw new ContextFullException($"context already has {DocsLimit} documents");
        }
        int length = CodePoints.Count(text);
        if (length > DocTextLimit) {
            throw new ContextFullException($"document text is {length} characters, over {DocTextLimit}");
        }
        EnsureRoom(context.TextLength + length);
        return context with { Docs = [.. context.Docs, ContextDoc.Create(name, text, today, Ids(context))] };
    }

    public static UserContext WithoutDoc(UserContext context, string id) {
        if (context.Docs.All(doc => doc.Id != id)) {
            throw new ContextDocNotFoundException($"no document with id {id}");
        }
        return context with { Docs = [.. context.Docs.Where(doc => doc.Id != id)] };
    }

    public static IReadOnlySet<string> Ids(UserContext context) {
        HashSet<string> ids = new(context.Docs.Select(doc => doc.Id), StringComparer.Ordinal);
        if (context.About.Length > 0) {
            ids.Add(aboutId);
        }
        return ids;
    }

    public static bool IsReserved(string id) => id == aboutId || ContextDoc.HasIdPrefix(id);

    private static void EnsureRoom(int total) {
        if (total > TextLimit) {
            throw new ContextFullException($"context text would be {total} characters, over {TextLimit}");
        }
    }
}
