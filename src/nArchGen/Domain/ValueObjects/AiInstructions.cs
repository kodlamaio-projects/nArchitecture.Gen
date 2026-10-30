namespace Domain.ValueObjects;

/// <summary>The skills and file-scoped rules <c>ai init --skills</c> writes; each has a template in <c>Templates/Ai</c>.</summary>
public static class AiInstructions
{
    /// <summary>Skill names; the template of each is <c>Skills/&lt;name&gt;.md.sbn</c>.</summary>
    public static readonly IReadOnlyList<string> Skills = ["add-entity", "add-command", "add-query", "add-business-rule"];

    /// <summary>Rules loaded only for matching files; the body of each is <c>Rules/&lt;name&gt;.md.sbn</c>.</summary>
    public static readonly IReadOnlyList<AiRule> Rules =
    [
        new(
            "application-features",
            "Commands, queries, mappers, validators and business rules of the Application layer",
            ["src/**/Application/Features/**"]
        ),
        new("webapi-controllers", "Controllers of the WebAPI layer", ["src/**/WebAPI/Controllers/**"]),
        new("persistence", "DbContexts, entity configurations and repositories of the Persistence layer", ["src/**/Persistence/**"]),
    ];
}

/// <param name="Globs">Paths relative to the solution root.</param>
public record AiRule(string Name, string Description, IReadOnlyList<string> Globs);
