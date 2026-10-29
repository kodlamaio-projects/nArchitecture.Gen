namespace ConsoleUI;

/// <summary>Process exit codes of nArchGen. Scripts and AI agents rely on them; do not renumber.</summary>
public static class CliExitCodes
{
    public const int Success = 0;

    /// <summary>The operation ran but failed: a business rule was violated or the upgraded solution does not build.</summary>
    public const int Failure = 1;

    /// <summary>Invalid or missing arguments, including a value that could only be asked for interactively.</summary>
    public const int UsageError = 2;

    /// <summary>Files or directories to be created already exist; nothing has been written.</summary>
    public const int Conflict = 3;

    /// <summary>An unexpected error, usually a bug in nArchGen.</summary>
    public const int UnexpectedError = 4;
}
