namespace Jobranger.Messaging.Fields;

internal sealed class Nfc(UnicodeTables tables) {
    private const int syllableBase = 0xAC00;
    private const int leadBase = 0x1100;
    private const int vowelBase = 0x1161;
    private const int trailBase = 0x11A7;
    private const int leadCount = 19;
    private const int vowelCount = 21;
    private const int trailCount = 28;
    private const int syllableBlock = vowelCount * trailCount;
    private const int syllableCount = leadCount * syllableBlock;
    private const int starterClass = 0;
    private const int noStarter = -1;

    public List<int> Normalize(IEnumerable<int> codes) {
        List<int> decomposed = [];
        foreach (int code in codes) {
            Decompose(decomposed, code);
        }
        Reorder(decomposed);
        return Compose(decomposed);
    }

    private void Decompose(List<int> output, int code) {
        if (!tables.Decompositions.TryGetValue(code, out int[]? parts)) {
            output.Add(code);
            return;
        }
        foreach (int part in parts) {
            Decompose(output, part);
        }
    }

    private void Reorder(List<int> codes) {
        int start = 0;
        while (start < codes.Count) {
            int end = start;
            while (end < codes.Count && Class(codes[end]) != starterClass) {
                end++;
            }
            if (end - start > 1) {
                List<int> ordered = [.. codes.GetRange(start, end - start).OrderBy(Class)];
                codes.RemoveRange(start, ordered.Count);
                codes.InsertRange(start, ordered);
            }
            start = end + 1;
        }
    }

    private List<int> Compose(List<int> codes) {
        List<int> output = [];
        int starter = noStarter;
        int lastClass = starterClass;
        foreach (int code in codes) {
            int codeClass = Class(code);
            bool reachable = starter != noStarter && (lastClass == starterClass || lastClass < codeClass);
            if (reachable && TryCompose(output[starter], code, out int composite)) {
                output[starter] = composite;
                continue;
            }
            if (codeClass == starterClass) {
                starter = output.Count;
            }
            lastClass = codeClass;
            output.Add(code);
        }
        return output;
    }

    private bool TryCompose(int first, int second, out int composite) {
        int lead = first - leadBase;
        int vowel = second - vowelBase;
        if (lead is >= 0 and < leadCount && vowel is >= 0 and < vowelCount) {
            composite = syllableBase + (((lead * vowelCount) + vowel) * trailCount);
            return true;
        }
        int syllable = first - syllableBase;
        int trail = second - trailBase;
        if (syllable is >= 0 and < syllableCount && syllable % trailCount == 0 && trail is > 0 and < trailCount) {
            composite = first + trail;
            return true;
        }
        return tables.Compositions.TryGetValue((first, second), out composite);
    }

    private int Class(int code) => tables.CombiningClasses.TryFind(code, out int value) ? value : starterClass;
}
