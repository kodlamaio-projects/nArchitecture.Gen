namespace ConsoleUI.Output;

/// <summary>
/// The document <c>--json</c> writes to stdout, for successful and failed runs alike. Its field names are part of the CLI
/// contract scripts and AI agents rely on; add fields, do not rename or remove them.
/// </summary>
public sealed class CliResult
{
    /// <summary>The command that ran, such as <c>generate crud</c>.</summary>
    public required string Command { get; init; }

    /// <summary><c>success</c> or <c>error</c>.</summary>
    public string Status => ExitCode == CliExitCodes.Success ? "success" : "error";

    /// <summary>The process exit code, see <see cref="CliExitCodes"/>.</summary>
    public int ExitCode { get; init; }

    /// <summary>Absolute paths of the files the command created.</summary>
    public IReadOnlyCollection<string> Created { get; init; } = [];

    /// <summary>Absolute paths of existing files the command changed.</summary>
    public IReadOnlyCollection<string> Updated { get; init; } = [];

    /// <summary>Steps that were skipped or need attention; the command still succeeded.</summary>
    public IReadOnlyCollection<string> Warnings { get; init; } = [];

    /// <summary>Why the command failed; <c>null</c> on success.</summary>
    public CliError? Error { get; init; }

    /// <summary>Command specific values, such as the report file of <c>upgrade</c>.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }

    public static IReadOnlyCollection<string> FullPaths(IEnumerable<string>? paths) =>
        paths?.Select(path => Path.GetFullPath(path)).Distinct().ToArray() ?? [];
}

/// <param name="Type"><c>usage</c>, <c>conflict</c>, <c>failure</c> or <c>unexpected</c>.</param>
/// <param name="Message">Human readable reason.</param>
/// <param name="Paths">Files involved in the error, such as the existing files of a conflict.</param>
public sealed record CliError(string Type, string Message, IReadOnlyCollection<string> Paths);
