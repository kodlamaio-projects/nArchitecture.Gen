namespace Core.CrossCuttingConcerns.Exceptions;

/// <summary>
/// Thrown when an operation would overwrite existing files or directories that the caller did not allow to be replaced.
/// </summary>
public class ConflictException : Exception
{
    public IReadOnlyCollection<string> ConflictingPaths { get; }

    public ConflictException(string message, IEnumerable<string> conflictingPaths)
        : base(message)
    {
        ConflictingPaths = conflictingPaths.ToArray();
    }
}
