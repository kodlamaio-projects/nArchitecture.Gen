using System.Diagnostics;
using System.Text;

namespace Core.CodeGen.CommandLine;

public sealed record ProcessResult(int ExitCode, string Output);

public static class ProcessRunner
{
    /// <summary>Runs an executable, waits for it and returns the exit code with the combined stdout/stderr text.</summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default
    )
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        StringBuilder output = new();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                lock (output)
                    output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                lock (output)
                    output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, output.ToString());
    }
}
