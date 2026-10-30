using System.Text.Json;
using ConsoleUI;
using Domain.ValueObjects;

namespace NArchGen.Cli.Tests;

/// <summary><c>ai init</c> writes AGENTS.md, plus one file per selected tool that points to it, into a solution root.</summary>
public class AiInitTests
{
    private static string[] strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    /// <summary>A solution root: a solution file and one project under src/ with a DbContext.</summary>
    private static SampleProject solution()
    {
        SampleProject solution = new();
        solution.Write("Todo.sln", "");
        solution.Write("src/todo/Persistence/Contexts/BaseDbContext.cs", "namespace Persistence.Contexts;\n");
        return solution;
    }

    [Fact]
    public async Task Skills_and_file_scoped_rules_are_written_in_the_format_of_each_tool()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude,copilot,cursor", "--skills");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        foreach (string skill in AiInstructions.Skills)
        {
            string content = solution.Read($".claude/skills/{skill}/SKILL.md");
            Assert.StartsWith($"---{Environment.NewLine}name: {skill}{Environment.NewLine}description: ", content.ReplaceLineEndings());
            Assert.DoesNotContain("{{", content);
            Assert.Contains("src/todo/", content);
        }
        // Copilot and Cursor read .claude/skills as well, so the skills are not written twice.
        Assert.False(Directory.Exists(solution.Combine(".agents")));

        Assert.Contains("  - \"src/**/Persistence/**\"", solution.Read(".claude/rules/persistence.md"));
        Assert.Contains("applyTo: \"src/**/Persistence/**\"", solution.Read(".github/instructions/persistence.instructions.md"));
        string cursorRule = solution.Read(".cursor/rules/persistence.mdc");
        Assert.Contains("globs: src/**/Persistence/**", cursorRule);
        Assert.Contains("alwaysApply: false", cursorRule);
        Assert.Contains("# Persistence", cursorRule);

        string agents = solution.Read("AGENTS.md");
        Assert.Contains("## Skills and file-scoped rules", agents);
        Assert.Contains("`.github/instructions/persistence.instructions.md` (Copilot)", agents);
    }

    [Theory]
    [InlineData("claude", ".claude/skills")]
    [InlineData("codex", ".agents/skills")]
    [InlineData("cursor", ".agents/skills")]
    [InlineData("copilot,cursor", ".agents/skills")]
    [InlineData("claude,cursor", ".claude/skills")]
    [InlineData("claude,codex", ".claude/skills,.agents/skills")]
    public void Skills_go_to_the_folders_the_selected_tools_read(string tools, string expected)
    {
        AiTool[] selected = [.. tools.Split(',').Select(tool => Enum.Parse<AiTool>(tool, ignoreCase: true))];

        Assert.Equal(expected.Split(','), AiToolFiles.SkillRootsOf(selected));
    }

    [Fact]
    public async Task Skills_are_not_written_without_the_skills_option_and_a_terminal()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(solution.Combine(".claude")));
        Assert.DoesNotContain("## Skills", solution.Read("AGENTS.md"));
    }

    [Fact]
    public async Task Skills_without_a_tool_that_reads_them_fail_with_usage_error()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--custom-file", "AI_RULES", "--skills");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.False(File.Exists(solution.Combine("AGENTS.md")));
    }

    [Fact]
    public async Task Writes_agents_md_and_the_files_of_the_selected_tools()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(
            solution.Path,
            "ai",
            "init",
            "--tools",
            "claude,codex,copilot,cursor",
            "--json"
        );

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("ai init", json.RootElement.GetProperty("command").GetString());
        string[] expected = ["AGENTS.md", "CLAUDE.md", ".github/copilot-instructions.md", ".cursor/rules/nArchitecture.mdc"];
        Assert.Equal(
            expected.Select(path => Path.GetFullPath(solution.Combine(path))).Order(),
            strings(json.RootElement.GetProperty("created")).Order()
        );

        string agents = solution.Read("AGENTS.md");
        Assert.Contains("Todo is built on", agents);
        Assert.Contains("src/todo/", agents);
        Assert.Contains("`BaseDbContext`", agents);
        Assert.Contains("Codex reads this file directly.", agents);
        Assert.Contains("`.cursor/rules/nArchitecture.mdc` (Cursor)", agents);
        Assert.DoesNotContain("{{", agents);
        Assert.Contains("@AGENTS.md", solution.Read("CLAUDE.md"));
    }

    [Fact]
    public async Task Without_tools_and_a_terminal_writes_only_agents_md()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(solution.Combine("AGENTS.md")));
        Assert.False(File.Exists(solution.Combine("CLAUDE.md")));
        Assert.DoesNotContain("## AI tool files", solution.Read("AGENTS.md"));
    }

    [Fact]
    public async Task Custom_file_gets_the_md_extension_and_a_warning_when_it_had_another_one()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--custom-file", "docs/ai-rules.txt", "--json");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(solution.Combine("docs/ai-rules.md")));
        Assert.False(File.Exists(solution.Combine("docs/ai-rules.txt")));
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        Assert.Contains(strings(json.RootElement.GetProperty("warnings")), warning => warning.Contains("\".txt\" is not supported"));
        Assert.Contains("`docs/ai-rules.md` (Custom)", solution.Read("AGENTS.md"));
    }

    [Theory]
    [InlineData("AI_RULES", "AI_RULES.md", false)]
    [InlineData("AI_RULES.md", "AI_RULES.md", false)]
    [InlineData("AI_RULES.MD", "AI_RULES.MD", false)]
    [InlineData("AI_RULES.txt", "AI_RULES.md", true)]
    [InlineData("docs\\rules.mdc", "docs/rules.md", true)]
    [InlineData(".airules", ".airules.md", false)]
    public void Custom_path_is_normalized_to_md(string customPath, string expected, bool warns)
    {
        string path = AiToolFiles.NormalizeCustomPath(customPath, out string? warning);

        Assert.Equal(expected, path);
        Assert.Equal(warns, warning is not null);
    }

    [Fact]
    public async Task Custom_tool_without_a_file_name_fails_with_usage_error()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "custom");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("--custom-file", result.StandardError);
        Assert.False(File.Exists(solution.Combine("AGENTS.md")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("AGENTS")]
    public async Task Custom_file_outside_the_solution_or_clashing_with_agents_md_fails(string customFile)
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--custom-file", customFile);

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.False(File.Exists(solution.Combine("AGENTS.md")));
    }

    [Fact]
    public async Task Unknown_tool_fails_with_usage_error_and_lists_the_choices()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude,windsurf");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("claude, codex, copilot, cursor, custom", result.StandardError);
    }

    [Fact]
    public async Task Outside_a_solution_root_fails_with_usage_error()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "ai", "init");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.False(File.Exists(project.Combine("AGENTS.md")));
    }

    [Fact]
    public async Task Existing_file_is_a_conflict_unless_forced()
    {
        using SampleProject solution = AiInitTests.solution();
        solution.Write("AGENTS.md", "hand written");

        NArchGenResult conflict = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude");

        Assert.Equal(CliExitCodes.Conflict, conflict.ExitCode);
        Assert.Equal("hand written", solution.Read("AGENTS.md"));
        Assert.False(File.Exists(solution.Combine("CLAUDE.md")));

        NArchGenResult forced = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude", "--force", "--json");

        Assert.Equal(CliExitCodes.Success, forced.ExitCode);
        using JsonDocument json = JsonDocument.Parse(forced.StandardOutput);
        Assert.Equal([Path.GetFullPath(solution.Combine("AGENTS.md"))], strings(json.RootElement.GetProperty("updated")));
        Assert.Contains("# AGENTS.md", solution.Read("AGENTS.md"));
    }

    [Fact]
    public async Task Dry_run_writes_nothing()
    {
        using SampleProject solution = AiInitTests.solution();

        NArchGenResult result = await NArchGenProcess.RunAsync(solution.Path, "ai", "init", "--tools", "claude", "--dry-run", "--json");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        Assert.Contains(
            Path.GetFullPath(solution.Combine("CLAUDE.md")),
            strings(json.RootElement.GetProperty("details").GetProperty("plannedCreated"))
        );
        Assert.False(File.Exists(solution.Combine("AGENTS.md")));
        Assert.False(File.Exists(solution.Combine("CLAUDE.md")));
    }
}
