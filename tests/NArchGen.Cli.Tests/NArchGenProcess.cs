using System.Diagnostics;

namespace NArchGen.Cli.Tests;

public sealed record NArchGenResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs the built nArchGen tool the way a script or an AI agent does: as a child process without a terminal.</summary>
public static class NArchGenProcess
{
    private static string toolPath => Path.Combine(AppContext.BaseDirectory, "nArchGen.dll");

    public static async Task<NArchGenResult> RunAsync(string workingDirectory, params string[] args)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(toolPath);
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        using Process process = Process.Start(startInfo)!;
        process.StandardInput.Close();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(1));
        await process.WaitForExitAsync(timeout.Token);

        return new NArchGenResult(process.ExitCode, await standardOutput, await standardError);
    }
}
