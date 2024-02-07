namespace Core.CrossCuttingConcerns.Exceptions;

public class BusinessException : Exception
{
    public BusinessException(string message)
        : base(message) { }

    public BusinessException()
        : base() { }

    public BusinessException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
