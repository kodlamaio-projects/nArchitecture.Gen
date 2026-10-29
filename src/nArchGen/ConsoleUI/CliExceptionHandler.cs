using System.Reflection;
using ConsoleUI.Exceptions;
using ConsoleUI.Output;
using Core.CrossCuttingConcerns.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI;

/// <summary>
/// Reports a failed command on stderr, and as a <see cref="CliResult"/> on stdout in <c>--json</c> mode, and maps it to one of
/// the <see cref="CliExitCodes"/>.
/// </summary>
public static class CliExceptionHandler
{
    public static int Handle(Exception exception, IAnsiConsole errorConsole)
    {
        exception = unwrap(exception);
        int exitCode = GetExitCode(exception);

        switch (exception)
        {
            case ConflictException conflictException:
                errorConsole.MarkupLine($"[red]Error:[/] {conflictException.Message.EscapeMarkup()}");
                foreach (string path in conflictException.ConflictingPaths)
                    errorConsole.MarkupLine($"  [yellow]{path.EscapeMarkup()}[/]");
                break;
            case UsageException or CommandAppException:
                errorConsole.MarkupLine($"[red]Error:[/] {exception.Message.EscapeMarkup()}");
                errorConsole.MarkupLine("Run with [blue]--help[/] to see the arguments and options.");
                break;
            case BusinessException:
                errorConsole.MarkupLine($"[red]Error:[/] {exception.Message.EscapeMarkup()}");
                break;
            default:
                errorConsole.MarkupLine(
                    "[red]Unexpected error.[/] Please report it at https://github.com/kodlamaio-projects/nArchitecture.Gen/issues"
                );
                errorConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                break;
        }

        if (CliOutput.IsJson)
            CliOutput.WriteJson(
                new CliResult
                {
                    Command = CliOutput.CommandName,
                    ExitCode = exitCode,
                    Error = GetError(exception),
                }
            );

        return exitCode;
    }

    public static CliError GetError(Exception exception)
    {
        exception = unwrap(exception);
        return exception switch
        {
            ConflictException conflictException => new CliError("conflict", exception.Message, conflictException.ConflictingPaths),
            UsageException or CommandAppException => new CliError("usage", exception.Message, []),
            BusinessException => new CliError("failure", exception.Message, []),
            _ => new CliError("unexpected", $"{exception.GetType().Name}: {exception.Message}", []),
        };
    }

    public static int GetExitCode(Exception exception) =>
        unwrap(exception) switch
        {
            ConflictException => CliExitCodes.Conflict,
            UsageException or CommandAppException => CliExitCodes.UsageError,
            BusinessException => CliExitCodes.Failure,
            _ => CliExitCodes.UnexpectedError,
        };

    private static Exception unwrap(Exception exception) =>
        exception switch
        {
            TargetInvocationException { InnerException: { } innerException } => unwrap(innerException),
            AggregateException { InnerExceptions.Count: 1 } wrapper => unwrap(wrapper.InnerExceptions[0]),
            _ => exception,
        };
}
