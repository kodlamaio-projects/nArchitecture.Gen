namespace Core.CodeGen.File;

/// <summary>
/// While recording, every write made through <see cref="CodeFile"/> is kept in memory instead of going to the disk, and
/// later reads see the recorded content. This is how generate commands run with <c>--dry-run</c>: they execute exactly
/// as usual, and the change set tells what they would have written.
/// </summary>
/// <remarks>
/// The active change set flows with the async context, so it must be started by the caller that then awaits the
/// generation, not inside an async iterator (the context is restored at every <c>yield</c>).
/// </remarks>
public sealed class FileChangeSet : IDisposable
{
    private static readonly AsyncLocal<FileChangeSet?> _active = new();
    private static readonly StringComparer _pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileChangeSet? _previous;
    private readonly Dictionary<string, string> _contents = new(_pathComparer);
    private readonly Dictionary<string, string?> _originalContents = new(_pathComparer);
    private readonly List<string> _writeOrder = [];

    private FileChangeSet(FileChangeSet? previous)
    {
        _previous = previous;
    }

    internal static FileChangeSet? Active => _active.Value;

    /// <summary>Starts recording writes in the current async context until the returned change set is disposed.</summary>
    public static FileChangeSet BeginRecording()
    {
        FileChangeSet changeSet = new(_active.Value);
        _active.Value = changeSet;
        return changeSet;
    }

    /// <summary>Files whose recorded content differs from the disk, in the order they were first written.</summary>
    public IReadOnlyList<FileChange> Changes =>
        _writeOrder
            .Select(path => new FileChange(path, _originalContents[path], _contents[path]))
            .Where(change => change.OriginalContent != change.NewContent)
            .ToArray();

    public void Dispose() => _active.Value = _previous;

    internal bool TryGetContent(string path, out string content) => _contents.TryGetValue(Path.GetFullPath(path), out content!);

    internal async Task RecordAsync(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_originalContents.ContainsKey(fullPath))
        {
            _originalContents[fullPath] = System.IO.File.Exists(fullPath) ? await System.IO.File.ReadAllTextAsync(fullPath) : null;
            _writeOrder.Add(fullPath);
        }
        _contents[fullPath] = content;
    }
}

/// <param name="Path">Absolute path of the file.</param>
/// <param name="OriginalContent">Content on the disk, or <c>null</c> when the file would be created.</param>
/// <param name="NewContent">Content that would be written.</param>
public sealed record FileChange(string Path, string? OriginalContent, string NewContent)
{
    public bool IsCreated => OriginalContent is null;
}
