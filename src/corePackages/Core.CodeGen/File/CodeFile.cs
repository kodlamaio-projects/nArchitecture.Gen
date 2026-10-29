using System.Text;

namespace Core.CodeGen.File;

/// <summary>
/// File access for the files code generation reads and changes in the target project. It goes to the disk, or to the
/// active <see cref="FileChangeSet"/> while one is recording (<c>--dry-run</c>). Template files can be read directly.
/// </summary>
public static class CodeFile
{
    public static bool Exists(string path) => FileChangeSet.Active?.TryGetContent(path, out _) == true || System.IO.File.Exists(path);

    public static async Task<string> ReadAllTextAsync(string path) =>
        FileChangeSet.Active is { } changeSet && changeSet.TryGetContent(path, out string content)
            ? content
            : await System.IO.File.ReadAllTextAsync(path);

    public static async Task<string[]> ReadAllLinesAsync(string path) =>
        FileChangeSet.Active is { } changeSet && changeSet.TryGetContent(path, out string content)
            ? SplitLines(content)
            : await System.IO.File.ReadAllLinesAsync(path);

    public static async Task WriteAllTextAsync(string path, string content)
    {
        if (FileChangeSet.Active is { } changeSet)
        {
            await changeSet.RecordAsync(path, content);
            return;
        }

        string directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        // Keep the byte order mark of an existing file, so an injected line does not also show up as a first-line change.
        await System.IO.File.WriteAllTextAsync(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: await hasUtf8BomAsync(path))
        );
    }

    private static async Task<bool> hasUtf8BomAsync(string path)
    {
        if (!System.IO.File.Exists(path))
            return false;

        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] start = new byte[preamble.Length];
        await using FileStream stream = System.IO.File.OpenRead(path);
        int read = await stream.ReadAtLeastAsync(start, start.Length, throwOnEndOfStream: false);
        return read == preamble.Length && start.SequenceEqual(preamble);
    }

    /// <summary>
    /// Writes each line followed by a line break. An existing file keeps its line endings (\n or \r\n), so injecting a line
    /// does not rewrite the whole file; a new one uses <see cref="Environment.NewLine"/>.
    /// </summary>
    public static async Task WriteAllLinesAsync(string path, IEnumerable<string> lines)
    {
        string newLine =
            Exists(path) && await ReadAllTextAsync(path) is { } content && content.Contains('\n')
                ? content.Contains("\r\n")
                    ? "\r\n"
                    : "\n"
                : Environment.NewLine;
        await WriteAllTextAsync(path, string.Concat(lines.Select(line => line + newLine)));
    }

    /// <summary>Splits like <see cref="System.IO.File.ReadAllLines(string)"/>: on \r\n, \n or \r, without a trailing empty line.</summary>
    internal static string[] SplitLines(string content)
    {
        List<string> lines = [];
        using StringReader reader = new(content);
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines.ToArray();
    }
}
