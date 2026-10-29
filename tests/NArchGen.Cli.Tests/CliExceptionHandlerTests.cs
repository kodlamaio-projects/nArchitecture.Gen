using System.Reflection;
using ConsoleUI;
using ConsoleUI.Exceptions;
using Core.CrossCuttingConcerns.Exceptions;

namespace NArchGen.Cli.Tests;

public class CliExceptionHandlerTests
{
    public static TheoryData<Exception, int> Cases =>
        new()
        {
            { new UsageException("missing"), CliExitCodes.UsageError },
            { new ConflictException("exists", ["a.cs"]), CliExitCodes.Conflict },
            { new BusinessException("rule"), CliExitCodes.Failure },
            { new InvalidOperationException("bug"), CliExitCodes.UnexpectedError },
            { new TargetInvocationException(new ConflictException("exists", ["a.cs"])), CliExitCodes.Conflict },
            { new AggregateException(new UsageException("missing")), CliExitCodes.UsageError },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_exceptions_to_exit_codes(Exception exception, int expectedExitCode) =>
        Assert.Equal(expectedExitCode, CliExceptionHandler.GetExitCode(exception));
}
