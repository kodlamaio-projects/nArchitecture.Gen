namespace Domain.ValueObjects;

/// <summary>AI coding tools <c>ai init</c> can write an instruction file for. Every one of them is pointed at AGENTS.md.</summary>
public enum AiTool
{
    Claude,
    Codex,
    Copilot,
    Cursor,
    Custom,
}
