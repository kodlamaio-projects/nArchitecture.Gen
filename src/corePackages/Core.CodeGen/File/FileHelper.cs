namespace Core.CodeGen.File;

public static class FileHelper
{
    public static async Task CreateFileAsync(string filePath, string fileContent)
    {
        string directory = Path.GetDirectoryName(filePath)!;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using StreamWriter sw = System.IO.File.CreateText(filePath);
        await sw.WriteAsync(fileContent);
    }

    public static async Task RemoveLinesAsync(string filePath, Func<string, bool> predicate)
    {
        IEnumerable<string> fileLines = await System.IO.File.ReadAllLinesAsync(filePath);
        fileLines = fileLines.Where(line => !predicate(line));
        await System.IO.File.WriteAllLinesAsync(filePath, fileLines);
    }

    public static async Task RemoveContentAsync(string filePath, IEnumerable<string> contents)
    {
        string fileContent = await System.IO.File.ReadAllTextAsync(filePath);
        foreach (string content in contents)
            fileContent = fileContent.Replace(content, string.Empty);
        await System.IO.File.WriteAllTextAsync(filePath, fileContent);
    }
}
