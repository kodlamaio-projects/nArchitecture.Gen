using System.Text.RegularExpressions;
using Core.CodeGen.Code.CSharp.ValueObjects;

namespace Core.CodeGen.Code.CSharp;

public static class CSharpCodeReader
{
    public static async Task<string> ReadClassNameAsync(string filePath)
    {
        string fileContent = await System.IO.File.ReadAllTextAsync(filePath);
        const string pattern = @"class\s+(\w+)";

        Match match = Regex.Match(fileContent, pattern);
        if (!match.Success)
            return string.Empty;

        return match.Groups[1].Value;
    }

    public static async Task<string> ReadBaseClassNameAsync(string filePath)
    {
        string fileContent = await System.IO.File.ReadAllTextAsync(filePath);
        const string pattern = @"class\s+\w+\s*:?\s*(\w+)";

        Match match = Regex.Match(fileContent, pattern);
        if (!match.Success)
            return string.Empty;

        return match.Groups[1].Value;
    }

    public static async Task<ICollection<string>> ReadBaseClassGenericArgumentsAsync(
        string filePath
    )
    {
        string fileContent = await System.IO.File.ReadAllTextAsync(filePath);
        const string pattern = @"class\s+\w+\s*:?\s*(\w+)\s*<([\w,\s]+)>";

        Match match = Regex.Match(fileContent, pattern);
        if (!match.Success)
            return new List<string>();
        string[] genericArguments = match.Groups[2].Value.Split(',');

        return genericArguments.Select(genericArgument => genericArgument.Trim()).ToArray();
    }

    public static async Task<ICollection<PropertyInfo>> ReadClassPropertiesAsync(string filePath)
    {
        string fileContent = await System.IO.File.ReadAllTextAsync(filePath);
        const string pattern =
            @"(public|protected|internal|protected internal|private protected|private)?\s+(const|static)?\s*(\w+)\s+(\w+)\s*\{[^}]+\}";

        MatchCollection matches = Regex.Matches(fileContent, pattern);
        List<PropertyInfo> result = new();
        foreach (Match match in matches)
        {
            string accessModifier = match.Groups[1].Value.Trim();
            PropertyInfo propertyInfo =
                new()
                {
                    AccessModifier = string.IsNullOrEmpty(accessModifier)
                        ? "private"
                        : accessModifier,
                    TypeName = match.Groups[3].Value,
                    Name = match.Groups[4].Value
                };
            result.Add(propertyInfo);
        }

        return result;
    }
}
