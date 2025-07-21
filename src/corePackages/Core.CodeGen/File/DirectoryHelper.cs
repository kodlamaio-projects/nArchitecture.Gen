using System.Reflection;
using Core.CrossCuttingConcerns.Helpers;

namespace Core.CodeGen.File;

public static class DirectoryHelper
{
    public static string AssemblyDirectory
    {
        get
        {
            string codeBase = PlatformHelper.GetDirectoryHeader() + Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            string assemblyDir = Path.GetDirectoryName(path)!;
            
            // Check if we're running from the build output directory (bin/Debug/net9.0)
            // If so, navigate back to the source directory to find templates
            if (assemblyDir.Contains(Path.Combine("bin", "Debug")) || assemblyDir.Contains(Path.Combine("bin", "Release")))
            {
                // Navigate from bin/Debug/net9.0 back to the project root
                string? projectRoot = FindProjectRoot(assemblyDir);
                if (projectRoot != null)
                {
                    string templatesPath = Path.Combine(projectRoot, "src", "NArchitecture.Gen", "core", "Domain", "Features", "TemplateManagement");
                    if (System.IO.Directory.Exists(templatesPath))
                    {
                        return templatesPath;
                    }
                }
            }
            
            return assemblyDir;
        }
    }
    
    private static string? FindProjectRoot(string startPath)
    {
        string? currentDir = startPath;
        while (currentDir != null)
        {
            // Look for solution file or other project indicators
            if (System.IO.File.Exists(Path.Combine(currentDir, "NArchitecture.Gen.slnx")) ||
                System.IO.Directory.Exists(Path.Combine(currentDir, "src", "NArchitecture.Gen")))
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return null;
    }

    public static ICollection<string> GetFilesInDirectoryTree(string root, string searchPattern)
    {
        List<string> files = [];

        Stack<string> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            files.AddRange(collection: Directory.GetFiles(dir, searchPattern));

            foreach (string subDir in Directory.GetDirectories(dir))
                stack.Push(subDir);
        }

        return files;
    }

    public static ICollection<string> GetDirsInDirectoryTree(string root, string searchPattern)
    {
        List<string> dirs = [];

        Stack<string> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            dirs.AddRange(collection: Directory.GetDirectories(dir, searchPattern));

            foreach (string subDir in Directory.GetDirectories(dir))
                stack.Push(subDir);
        }

        return dirs;
    }

    public static void DeleteDirectory(string path)
    {
        foreach (string subPath in Directory.EnumerateDirectories(path))
            DeleteDirectory(subPath);

        foreach (string filePath in Directory.EnumerateFiles(path))
        {
            FileInfo file = new(filePath) { Attributes = FileAttributes.Normal };
            file.Delete();
        }
        Directory.Delete(path);
    }
}
