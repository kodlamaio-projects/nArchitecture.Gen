namespace Domain.ValueObjects;

/// <summary>Where <c>ai init</c> writes the instruction file of each tool, relative to the solution root.</summary>
public static class AiToolFiles
{
    public const string Agents = "AGENTS.md";

    /// <summary>The file of <paramref name="tool"/>; <c>null</c> for Codex, which reads AGENTS.md itself, and for Custom.</summary>
    public static string? PathOf(AiTool tool) =>
        tool switch
        {
            AiTool.Claude => "CLAUDE.md",
            AiTool.Copilot => ".github/copilot-instructions.md",
            AiTool.Cursor => ".cursor/rules/nArchitecture.mdc",
            _ => null,
        };

    /// <summary>
    /// Skill folders for <paramref name="tools"/>. Claude reads <c>.claude/skills</c> and Codex <c>.agents/skills</c>; Copilot
    /// and Cursor read both, so <c>.agents/skills</c> is only added for them when Claude is not selected, otherwise they
    /// would load every skill twice.
    /// </summary>
    public static IReadOnlyList<string> SkillRootsOf(ICollection<AiTool> tools)
    {
        List<string> roots = [];
        if (tools.Contains(AiTool.Claude))
            roots.Add(".claude/skills");
        if (
            tools.Contains(AiTool.Codex)
            || (!tools.Contains(AiTool.Claude) && (tools.Contains(AiTool.Copilot) || tools.Contains(AiTool.Cursor)))
        )
            roots.Add(".agents/skills");
        return roots;
    }

    /// <summary>The file a file-scoped rule is written to for <paramref name="tool"/>; <c>null</c> when the tool has none.</summary>
    public static string? RulePathOf(AiTool tool, AiRule rule) =>
        tool switch
        {
            AiTool.Claude => $".claude/rules/{rule.Name}.md",
            AiTool.Copilot => $".github/instructions/{rule.Name}.instructions.md",
            AiTool.Cursor => $".cursor/rules/{rule.Name}.mdc",
            _ => null,
        };

    /// <summary>The front matter that limits <paramref name="rule"/> to its files, in the format of <paramref name="tool"/>.</summary>
    public static string RuleFrontMatterOf(AiTool tool, AiRule rule) =>
        tool switch
        {
            AiTool.Claude => $"---\npaths:\n{string.Concat(rule.Globs.Select(glob => $"  - \"{glob}\"\n"))}---\n",
            AiTool.Copilot => $"---\napplyTo: \"{string.Join(',', rule.Globs)}\"\n---\n",
            AiTool.Cursor => $"---\ndescription: {rule.Description}\nglobs: {string.Join(", ", rule.Globs)}\nalwaysApply: false\n---\n",
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "The tool has no file-scoped rules."),
        };

    /// <summary>
    /// Gives a custom file name the <c>.md</c> extension: it is added when missing, and any other extension is replaced,
    /// in which case <paramref name="warning"/> tells the user.
    /// </summary>
    public static string NormalizeCustomPath(string customPath, out string? warning)
    {
        warning = null;
        string path = customPath.Trim().Replace('\\', '/').TrimEnd('.');
        string fileName = System.IO.Path.GetFileName(path);
        string extension = System.IO.Path.GetExtension(fileName);

        // A dot file such as ".airules" has no extension, only a name.
        if (extension.Length == 0 || extension == fileName)
            return path + ".md";
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            return path;

        string normalizedPath = path[..^extension.Length] + ".md";
        warning = $"\"{extension}\" is not supported for the custom AI file; it has been written as \"{normalizedPath}\".";
        return normalizedPath;
    }
}
