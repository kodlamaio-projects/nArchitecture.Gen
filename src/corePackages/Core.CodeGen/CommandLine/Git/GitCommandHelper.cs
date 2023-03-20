using System.Diagnostics;

namespace Core.CodeGen.CommandLine.Git;

public static class GitCommandHelper
{
    public static Process GetGitProcess()
    {
        Process gitProcess = new();
        gitProcess.StartInfo.FileName = "git";
        gitProcess.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
        gitProcess.StartInfo.UseShellExecute = false;
        gitProcess.StartInfo.RedirectStandardOutput = true;
        gitProcess.StartInfo.RedirectStandardError = true;
        return gitProcess;
    }

    public static async Task RunAsync(string arguments)
    {
        Process command = GetGitProcess();
        command.StartInfo.Arguments = arguments;
        command.Start();
        await command.WaitForExitAsync();
    }

    public static async Task CommitChangesAsync(string message)
    {
        Process addCommand = GetGitProcess();
        addCommand.StartInfo.Arguments = "add .";
        addCommand.Start();
        await addCommand.WaitForExitAsync();

        await CommandLineHelper.RunCommandAsync($"git commit -m \"{message}\"");
    }
}
