using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class AiTemplateData : ITemplateData
{
    public required string SolutionName { get; set; }
    public ICollection<AiProject> Projects { get; set; } = [];

    /// <summary>Tool specific files written next to AGENTS.md, so AGENTS.md can list them.</summary>
    public ICollection<AiToolFile> ToolFiles { get; set; } = [];

    /// <summary>Folders the skills are written to, such as <c>.claude/skills</c>; empty without <c>--skills</c>.</summary>
    public ICollection<string> SkillRoots { get; set; } = [];

    /// <summary>File-scoped rule files, one per rule and tool; empty without <c>--skills</c>.</summary>
    public ICollection<AiToolFile> RuleFiles { get; set; } = [];

    public bool IsCodexSelected { get; set; }
    public bool HasTests { get; set; }
    public bool HasToolManifest { get; set; }
    public bool HasCsharpier { get; set; }
    public bool IsGeneratorLocalTool { get; set; }

    /// <summary>How to call nArchGen in this solution.</summary>
    public string GeneratorCommand => IsGeneratorLocalTool ? "dotnet nArchGen" : "nArchGen";

    public IReadOnlyList<string> SkillNames => AiInstructions.Skills;
}

public class AiProject
{
    public required string Name { get; set; }
    public ICollection<string> DbContexts { get; set; } = [];
}

/// <param name="Path">Relative to the solution root, with <c>/</c> separators.</param>
public record AiToolFile(AiTool Tool, string Path);
