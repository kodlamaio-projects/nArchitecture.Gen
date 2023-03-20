using System.Reflection;

namespace Core.CodeGen.File;

public static class DirectoryHelper
{
    public static string AssemblyDirectory
    {
        get
        {
            string codeBase = Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path)!;
        }
    }

    public static ICollection<string> GetFilesInDirectoryTree(string root, string searchPattern)
    {
        List<string> files = new();

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
        List<string> dirs = new();

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
