namespace ConsoleUI.Exceptions;

/// <summary>Thrown when the command line is missing a value or has an invalid one; maps to <see cref="CliExitCodes.UsageError"/>.</summary>
public class UsageException : Exception
{
    public UsageException(string message)
        : base(message) { }
}
