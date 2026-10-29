namespace Core.CodeGen.File;

public static class FileHelper
{
    public static async Task CreateFileAsync(string filePath, string fileContent)
    {
        await CodeFile.WriteAllTextAsync(filePath, fileContent);
    }

    public static async Task RemoveLinesAsync(string filePath, Func<string, bool> predicate)
    {
        IEnumerable<string> fileLines = await CodeFile.ReadAllLinesAsync(filePath);
        fileLines = fileLines.Where(line => !predicate(line));
        await CodeFile.WriteAllLinesAsync(filePath, fileLines);
    }

    /// <summary>
    /// Removes each snippet from the file. Snippets are written with <c>\n</c> line endings; the file is matched after
    /// normalizing its line endings and written back with the line endings it originally had.
    /// </summary>
    public static async Task RemoveContentAsync(string filePath, IEnumerable<string> contents)
    {
        string fileContent = await CodeFile.ReadAllTextAsync(filePath);
        bool usesCrLf = fileContent.Contains("\r\n");
        if (usesCrLf)
            fileContent = fileContent.Replace("\r\n", "\n");

        foreach (string content in contents)
            fileContent = fileContent.Replace(content.Replace("\r\n", "\n"), string.Empty);

        if (usesCrLf)
            fileContent = fileContent.Replace("\n", "\r\n");
        await CodeFile.WriteAllTextAsync(filePath, fileContent);
    }
}
