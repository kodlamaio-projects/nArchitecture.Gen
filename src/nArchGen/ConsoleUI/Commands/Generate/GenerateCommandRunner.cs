using Application.Features.Generate.Commands;
using ConsoleUI.Output;
using Core.Application.Commands;
using Core.CodeGen.File;
using Spectre.Console;

namespace ConsoleUI.Commands.Generate;

/// <summary>Runs a generate request, shows its progress and reports the result; with <c>--dry-run</c> nothing is written.</summary>
public static class GenerateCommandRunner
{
    public static async Task<int> RunAsync<TResponse>(
        string command,
        string status,
        GenerateCliCommandSettings settings,
        Func<IAsyncEnumerable<TResponse>> createStream
    )
        where TResponse : BaseStreamCommandResponse, IGeneratedFilesResponse
    {
        // Started here, around the whole run, because the recording flows with the async context into the handler.
        using FileChangeSet? changeSet = settings.DryRun ? FileChangeSet.BeginRecording() : null;
        TResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status,
            async setStatus =>
            {
                await foreach (TResponse result in createStream())
                {
                    last = result;
                    setStatus(result.CurrentStatusMessage);

                    if (result.OutputMessage is not null)
                        AnsiConsole.MarkupLine(result.OutputMessage);

                    if (result.LastOperationMessage is not null)
                        AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage}");

                    // A dry run lists what the change set recorded once it is done.
                    if (changeSet is not null)
                        continue;
                    writeFiles(":new_button: [green]Generated files:[/]", result.NewFilePathsResult);
                    writeFiles(":up_button: [green]Updated files:[/]", result.UpdatedFilePathsResult);
                }
            }
        );

        IReadOnlyCollection<string> warnings = last?.Warnings.ToArray() ?? [];
        if (changeSet is null)
        {
            CliOutput.WriteResult(
                new CliResult
                {
                    Command = command,
                    Created = CliResult.FullPaths(last?.NewFilePathsResult),
                    Updated = CliResult.FullPaths(last?.UpdatedFilePathsResult),
                    Warnings = warnings,
                }
            );
            return CliExitCodes.Success;
        }

        IReadOnlyList<FileChange> changes = changeSet.Changes;
        string[] plannedCreated = changes.Where(change => change.IsCreated).Select(change => change.Path).ToArray();
        Dictionary<string, string> diffs = changes
            .Where(change => !change.IsCreated)
            .ToDictionary(change => change.Path, change => UnifiedDiff.Create(change, displayPath(change.Path)));

        if (!CliOutput.IsJson)
            writeDryRun(plannedCreated, diffs);

        CliOutput.WriteResult(
            new CliResult
            {
                Command = command,
                Warnings = warnings,
                Details = new Dictionary<string, object?>
                {
                    ["dryRun"] = true,
                    ["plannedCreated"] = plannedCreated,
                    ["plannedUpdated"] = diffs.Keys.ToArray(),
                    ["diffs"] = diffs,
                },
            }
        );
        return CliExitCodes.Success;
    }

    private static void writeFiles(string title, ICollection<string>? filePaths)
    {
        if (filePaths is null)
            return;

        AnsiConsole.MarkupLine(title);
        foreach (string filePath in filePaths)
            CliOutput.WriteFilePath(filePath);
    }

    private static void writeDryRun(IReadOnlyCollection<string> plannedCreated, Dictionary<string, string> diffs)
    {
        writeFiles(":new_button: [yellow]Files that would be created:[/]", plannedCreated.ToArray());
        writeFiles(":up_button: [yellow]Files that would be updated:[/]", diffs.Keys.ToArray());

        foreach (string diff in diffs.Values)
        foreach (string line in diff.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string style = line switch
            {
                _ when line.StartsWith("+++") || line.StartsWith("---") => "bold",
                _ when line.StartsWith("@@") => "cyan",
                _ when line.StartsWith('+') => "green",
                _ when line.StartsWith('-') => "red",
                _ => "grey",
            };
            AnsiConsole.MarkupLine($"[{style}]{line.EscapeMarkup()}[/]");
        }

        AnsiConsole.MarkupLine(":memo: [yellow]Dry run: nothing has been written.[/]");
    }

    private static string displayPath(string path) => Path.GetRelativePath(Environment.CurrentDirectory, path).Replace('\\', '/');
}
