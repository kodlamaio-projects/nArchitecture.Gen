using System.Text;

namespace Core.CodeGen.File;

/// <summary>Line based unified diff (the <c>git diff</c> format) of a <see cref="FileChange"/>, for showing a dry run.</summary>
public static class UnifiedDiff
{
    /// <summary>Above this many line pairs the diff falls back to replacing the whole file instead of computing an LCS.</summary>
    private const long _maxLcsCells = 4_000_000;

    public static string Create(FileChange change, string displayPath, int contextLines = 3)
    {
        string[] originalLines = CodeFile.SplitLines(change.OriginalContent ?? string.Empty);
        string[] newLines = CodeFile.SplitLines(change.NewContent);
        List<(char Kind, string Line)> operations = diff(originalLines, newLines);

        StringBuilder builder = new();
        builder.Append("--- ").Append(change.IsCreated ? "/dev/null" : $"a/{displayPath}").Append('\n');
        builder.Append("+++ ").Append($"b/{displayPath}").Append('\n');

        // Line numbers in the original and the new file before each operation.
        int[] originalPositions = new int[operations.Count + 1];
        int[] newPositions = new int[operations.Count + 1];
        for (int i = 0; i < operations.Count; ++i)
        {
            originalPositions[i + 1] = originalPositions[i] + (operations[i].Kind == '+' ? 0 : 1);
            newPositions[i + 1] = newPositions[i] + (operations[i].Kind == '-' ? 0 : 1);
        }

        int next = 0;
        while (true)
        {
            int firstChange = operations.FindIndex(next, operation => operation.Kind != ' ');
            if (firstChange < 0)
                break;

            // Extend the hunk over changes that are separated by at most two context blocks.
            int lastChange = firstChange;
            for (int i = firstChange + 1; i < operations.Count; ++i)
            {
                if (operations[i].Kind == ' ')
                    continue;
                if (i - lastChange - 1 > 2 * contextLines)
                    break;
                lastChange = i;
            }

            int start = Math.Max(next, firstChange - contextLines);
            int end = Math.Min(operations.Count, lastChange + 1 + contextLines);
            int originalCount = originalPositions[end] - originalPositions[start];
            int newCount = newPositions[end] - newPositions[start];
            builder.Append(
                $"@@ -{hunkStart(originalPositions[start], originalCount)},{originalCount} +{hunkStart(newPositions[start], newCount)},{newCount} @@\n"
            );
            for (int i = start; i < end; ++i)
                builder.Append(operations[i].Kind).Append(operations[i].Line).Append('\n');

            next = end;
        }

        return builder.ToString();
    }

    private static int hunkStart(int linesBefore, int count) => count == 0 ? linesBefore : linesBefore + 1;

    private static List<(char Kind, string Line)> diff(string[] original, string[] modified)
    {
        List<(char, string)> operations = [];
        if ((long)original.Length * modified.Length > _maxLcsCells)
        {
            operations.AddRange(original.Select(line => ('-', line)));
            operations.AddRange(modified.Select(line => ('+', line)));
            return operations;
        }

        // lcs[i, j] = length of the longest common subsequence of original[i..] and modified[j..].
        int[,] lcs = new int[original.Length + 1, modified.Length + 1];
        for (int i = original.Length - 1; i >= 0; --i)
        for (int j = modified.Length - 1; j >= 0; --j)
            lcs[i, j] = original[i] == modified[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        int originalIndex = 0,
            modifiedIndex = 0;
        while (originalIndex < original.Length && modifiedIndex < modified.Length)
        {
            if (original[originalIndex] == modified[modifiedIndex])
            {
                operations.Add((' ', original[originalIndex++]));
                ++modifiedIndex;
            }
            else if (lcs[originalIndex + 1, modifiedIndex] >= lcs[originalIndex, modifiedIndex + 1])
                operations.Add(('-', original[originalIndex++]));
            else
                operations.Add(('+', modified[modifiedIndex++]));
        }
        while (originalIndex < original.Length)
            operations.Add(('-', original[originalIndex++]));
        while (modifiedIndex < modified.Length)
            operations.Add(('+', modified[modifiedIndex++]));

        return operations;
    }
}
