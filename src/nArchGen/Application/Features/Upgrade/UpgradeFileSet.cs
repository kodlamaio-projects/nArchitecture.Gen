using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Application.Features.Upgrade;

/// <summary>
/// In-memory view of the solution's text files. Steps read and write here; nothing touches the disk until
/// <see cref="FlushAsync"/> runs, which keeps <c>--dry-run</c> trivial and the steps unit-testable.
/// </summary>
public sealed class UpgradeFileSet
{
    private static readonly string[] _excludedDirectories = [".git", ".vs", "bin", "obj", "artifacts", "node_modules", "TestResults"];
    private static readonly string[] _textExtensions = [".cs", ".csproj", ".props", ".json", ".yml", ".yaml", ".md", ".config", ".sln"];

    private readonly Dictionary<string, string> _original = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deleted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SyntaxTree> _trees = new(StringComparer.OrdinalIgnoreCase);
    private SourceIndex? _index;

    public string Root { get; }

    public UpgradeFileSet(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public static async Task<UpgradeFileSet> LoadAsync(string root)
    {
        UpgradeFileSet set = new(root);
        foreach (string file in Directory.EnumerateFiles(set.Root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(set.Root, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(_excludedDirectories.Contains))
                continue;
            if (!_textExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;
            string text = await File.ReadAllTextAsync(file);
            set._original[file] = text;
            set._current[file] = text;
        }
        return set;
    }

    /// <summary>Builds a set from in-memory files; used by tests.</summary>
    public static UpgradeFileSet FromMemory(string root, IEnumerable<KeyValuePair<string, string>> files)
    {
        UpgradeFileSet set = new(root);
        foreach ((string path, string text) in files)
        {
            string full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(set.Root, path));
            set._original[full] = text;
            set._current[full] = text;
        }
        return set;
    }

    public IEnumerable<string> Files => _current.Keys.Where(f => !_deleted.Contains(f));

    public IEnumerable<string> FilesWithExtension(string extension) =>
        Files.Where(f => string.Equals(Path.GetExtension(f), extension, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> CSharpFiles => FilesWithExtension(".cs");

    public bool Exists(string path) => _current.ContainsKey(Path.GetFullPath(path)) && !_deleted.Contains(Path.GetFullPath(path));

    public string Read(string path) => _current[Path.GetFullPath(path)];

    public bool TryRead(string path, out string text)
    {
        string full = Path.GetFullPath(path);
        if (!_deleted.Contains(full) && _current.TryGetValue(full, out string? value))
        {
            text = value;
            return true;
        }
        text = string.Empty;
        return false;
    }

    public void Write(string path, string text)
    {
        string full = Path.GetFullPath(path);
        _current[full] = text;
        _deleted.Remove(full);
        _trees.Remove(full);
        _index = null;
    }

    public void Delete(string path)
    {
        string full = Path.GetFullPath(path);
        _deleted.Add(full);
        _trees.Remove(full);
        _index = null;
    }

    public bool IsChanged(string path)
    {
        string full = Path.GetFullPath(path);
        return _deleted.Contains(full) || !_original.TryGetValue(full, out string? original) || original != _current[full];
    }

    public IEnumerable<string> ChangedFiles => _current.Keys.Where(IsChanged);

    public SyntaxTree GetTree(string path)
    {
        string full = Path.GetFullPath(path);
        if (!_trees.TryGetValue(full, out SyntaxTree? tree))
            _trees[full] = tree = CSharpSyntaxTree.ParseText(_current[full], path: full);
        return tree;
    }

    public SourceIndex Index => _index ??= SourceIndex.Build(CSharpFiles.Select(f => new KeyValuePair<string, SyntaxTree>(f, GetTree(f))));

    /// <summary>Writes changed files and removes deleted ones. Empty directories left behind are removed too.</summary>
    public async Task<IReadOnlyList<string>> FlushAsync()
    {
        List<string> touched = [];
        foreach (string path in _deleted)
        {
            if (File.Exists(path))
                File.Delete(path);
            touched.Add(path);
            string? dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        foreach ((string path, string text) in _current)
        {
            if (_deleted.Contains(path))
                continue;
            if (_original.TryGetValue(path, out string? original) && original == text)
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text);
            touched.Add(path);
        }
        return touched;
    }
}
