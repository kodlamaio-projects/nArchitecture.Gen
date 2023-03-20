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
}
