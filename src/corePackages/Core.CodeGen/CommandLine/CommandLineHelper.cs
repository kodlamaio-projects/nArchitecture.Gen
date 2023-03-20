using System.Diagnostics;

namespace Core.CodeGen.CommandLine;

public static class CommandLineHelper
{
    public static string GetOSCommandLine() =>
        Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => "/bin/bash",
            PlatformID.MacOSX => "/bin/sh",
            _ => "cmd.exe"
        };

    public static async Task RunCommandAsync(string command)
    {
        try
        {
            Process process =
                new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = GetOSCommandLine(),
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

            process.Start();
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred while sending the command: " + ex.Message);
        }
    }
}
