using System.Text.RegularExpressions;
using Core.CodeGen.File;

namespace Core.CodeGen.Code.CSharp;

public static class CSharpCodeInjector
{
    public static async Task AddCodeLinesToMethodAsync(string filePath, string methodName, string[] codeLines)
    {
        var fileContent = (await CodeFile.ReadAllLinesAsync(filePath)).ToList();
        if (isAlreadyInjected(fileContent, codeLines))
            return;
        string methodStartRegex =
            @"((public|protected|internal|protected internal|private protected|private)\s+)?(static\s+)?(void|[a-zA-Z]+(<.*>)?)\s+\b"
            + methodName
            + @"\b\s*\(";
        const string scopeBlockStartRegex = @"\{";
        const string scopeBlockEndRegex = @"\}";

        int methodStartIndex = -1;
        int methodEndIndex = -1;
        int curlyBracketCountInMethod = 1;
        for (int i = 0; i < fileContent.Count; ++i)
        {
            Match methodStart = Regex.Match(input: fileContent[i], methodStartRegex);
            if (!methodStart.Success)
                continue;

            methodStartIndex = i;
            if (!Regex.Match(input: fileContent[i], pattern: @"\{").Success)
                for (int j = methodStartIndex + 1; j < fileContent.Count; ++j)
                {
                    if (!Regex.Match(input: fileContent[j], pattern: @"\{").Success)
                        continue;
                    methodStartIndex = j;
                    break;
                }
        }

        for (int i = methodStartIndex + 1; i < fileContent.Count; ++i)
        {
            if (Regex.Match(input: fileContent[i], scopeBlockStartRegex).Success)
                ++curlyBracketCountInMethod;
            if (Regex.Match(input: fileContent[i], scopeBlockEndRegex).Success)
                --curlyBracketCountInMethod;
            if (curlyBracketCountInMethod != 0)
                continue;

            methodEndIndex = i;

            for (int j = methodEndIndex - 1; j > methodStartIndex; --j)
            {
                if (Regex.Match(input: fileContent[j], scopeBlockEndRegex).Success)
                    break;
                if (Regex.Match(input: fileContent[j], pattern: @"\)\s+return").Success)
                    break;
                if (
                    Regex.Match(input: fileContent[j], pattern: @"\s+return").Success
                    && Regex.Match(input: fileContent[j - 1], pattern: @"(if|else if|else)\s*\(").Success
                )
                    break;

                if (Regex.Match(input: fileContent[j], pattern: @"\s+return").Success)
                {
                    methodEndIndex = j;
                    break;
                }
            }

            break;
        }

        if (methodStartIndex == -1 || methodEndIndex == -1)
            throw new Exception($"{methodName} not found in \"{filePath}\".");

        ICollection<string> methodContent = fileContent.Skip(methodStartIndex + 1).Take(methodEndIndex - 1 - methodStartIndex).ToArray();
        int minimumSpaceCountInMethod =
            methodContent.Count < 2
                ? fileContent[methodStartIndex].TakeWhile(char.IsWhiteSpace).Count() * 2
                : methodContent.Where(line => !string.IsNullOrEmpty(line)).Min(line => line.TakeWhile(char.IsWhiteSpace).Count());

        fileContent.InsertRange(methodEndIndex, collection: codeLines.Select(line => new string(' ', minimumSpaceCountInMethod) + line));
        await CodeFile.WriteAllLinesAsync(filePath, lines: fileContent.ToArray());
    }

    public static async Task AddCodeLinesAsPropertyAsync(string filePath, string[] codeLines)
    {
        string[] fileContent = await CodeFile.ReadAllLinesAsync(filePath);
        if (isAlreadyInjected(fileContent, codeLines))
            return;
        const string propertyStartRegex =
            @"(public|protected|internal|protected internal|private protected|private)?\s*(?:const|static|required)?\s+(\w+(<.*>)?)\s+(\w+)\s*(?:\{.*\}|=.+;)";

        int indexToAdd = -1;
        for (int i = 0; i < fileContent.Length; ++i)
        {
            Match propertyStart = Regex.Match(input: fileContent[i], propertyStartRegex);
            if (propertyStart.Success)
                indexToAdd = i;
        }

        int propertySpaceCountInClass;
        if (indexToAdd == -1)
        {
            const string classRegex = @"class\s+(\w+)";

            for (int i = 0; i < fileContent.Length; ++i)
            {
                Match propertyStart = Regex.Match(input: fileContent[i], classRegex);
                if (propertyStart.Success)
                    indexToAdd = i;

                if (!Regex.Match(input: fileContent[i], pattern: @"\{").Success)
                    for (int j = indexToAdd + 1; j < fileContent.Length; ++j)
                    {
                        if (!Regex.Match(input: fileContent[j], pattern: @"\{").Success)
                            continue;
                        indexToAdd = j;
                        break;
                    }
            }

            propertySpaceCountInClass = fileContent[indexToAdd].TakeWhile(char.IsWhiteSpace).Count() * 2;
        }
        else
        {
            propertySpaceCountInClass = fileContent[indexToAdd].TakeWhile(char.IsWhiteSpace).Count();
        }

        List<string> updatedFileContent = new(fileContent);
        updatedFileContent.InsertRange(
            index: indexToAdd + 1,
            collection: codeLines.Select(line => new string(' ', propertySpaceCountInClass) + line)
        );

        await CodeFile.WriteAllLinesAsync(filePath, lines: updatedFileContent.ToArray());
    }

    public static async Task AddCodeLinesToRegionAsync(string filePath, IEnumerable<string> linesToAdd, string regionName)
    {
        var fileContent = (await CodeFile.ReadAllLinesAsync(filePath)).ToList();
        string regionStartRegex = @$"^\s*#region\s*{regionName}\s*";
        const string regionEndRegex = @"^\s*#endregion\s*.*";

        bool isInRegion = false;
        int indexToAdd;
        for (indexToAdd = 0; indexToAdd < fileContent.Count; indexToAdd++)
        {
            string fileLine = fileContent[indexToAdd];

            if (Regex.Match(fileLine, regionStartRegex).Success)
            {
                isInRegion = true;
                continue;
            }

            if (!isInRegion)
                continue;
            if (!Regex.Match(fileLine, regionEndRegex).Success)
                continue;

            int minimumSpaceCountInRegion = fileContent[indexToAdd].TakeWhile(char.IsWhiteSpace).Count();

            string previousLine = fileContent[index: indexToAdd - 1];
            if (Regex.Match(previousLine, regionStartRegex).Success)
            {
                fileContent.Insert(indexToAdd, string.Empty);
                indexToAdd += 2;
            }

            if (!string.IsNullOrEmpty(previousLine))
                fileContent.Insert(index: indexToAdd, string.Empty);

            fileContent.InsertRange(
                index: indexToAdd,
                collection: linesToAdd.Select(line => new string(' ', minimumSpaceCountInRegion) + line)
            );
            await CodeFile.WriteAllLinesAsync(filePath, fileContent);
            break;
        }
    }

    public static async Task AddUsingToFile(string filePath, IEnumerable<string> usingLines)
    {
        var fileContent = (await CodeFile.ReadAllLinesAsync(filePath)).ToList();

        IEnumerable<string> usingLinesToAdd = usingLines.Where(usingLine => !fileContent.Contains(usingLine));

        Regex usingRegex = new(@"^using\s+.*;$");
        int indexToAdd = 0;
        for (int i = 0; i < fileContent.Count; ++i)
        {
            string fileLine = fileContent[i];
            if (usingRegex.IsMatch(fileLine))
                continue;
            indexToAdd = i;
            break;
        }

        fileContent.InsertRange(indexToAdd, usingLinesToAdd);
        await CodeFile.WriteAllLinesAsync(filePath, fileContent);
    }

    public static async Task AddMethodToClass(string filePath, string className, string[] codeLines)
    {
        var fileContent = (await CodeFile.ReadAllLinesAsync(filePath)).ToList();
        if (isAlreadyInjected(fileContent, codeLines))
            return;
        Regex classStartRegex = new(
            @$"((public|protected|internal|protected internal|private protected|private)\s+)?(static\s+)?\s+\b{className}"
        );
        Regex scopeBlockStartRegex = new(@"\{");
        Regex scopeBlockEndRegex = new(@"\}");

        int classStartIndex = -1;
        int classEndIndex = -1;
        for (int i = 0; i < fileContent.Count; ++i)
        {
            string fileLine = fileContent[i];

            Match methodStart = classStartRegex.Match(input: fileLine);
            if (!methodStart.Success)
                continue;

            classStartIndex = i;
            if (!scopeBlockStartRegex.Match(fileLine).Success)
                for (int j = classStartIndex + 1; j < fileContent.Count; ++j)
                {
                    if (!scopeBlockStartRegex.Match(fileContent[j]).Success)
                        continue;
                    classStartIndex = j;
                    break;
                }
            break;
        }

        int curlyBracketCountInClass = 1;
        for (int i = classStartIndex + 1; i < fileContent.Count; ++i)
        {
            if (scopeBlockStartRegex.Match(input: fileContent[i]).Success)
                ++curlyBracketCountInClass;
            if (scopeBlockEndRegex.Match(input: fileContent[i]).Success)
                --curlyBracketCountInClass;
            if (curlyBracketCountInClass != 0)
                continue;

            classEndIndex = i;
            break;
        }
        if (classStartIndex == -1 || classEndIndex == -1)
            throw new Exception($"{className} not found in \"{filePath}\".");

        ICollection<string> classContent = fileContent.Skip(classStartIndex + 1).Take(classEndIndex - 1 - classStartIndex).ToArray();

        int minimumSpaceCountInClass =
            classContent.Count < 2
                ? fileContent[classStartIndex].TakeWhile(char.IsWhiteSpace).Count() * 2
                : classContent.Where(line => !string.IsNullOrEmpty(line)).Min(line => line.TakeWhile(char.IsWhiteSpace).Count());

        fileContent.InsertRange(classEndIndex, collection: codeLines.Select(line => new string(' ', minimumSpaceCountInClass) + line));
        await CodeFile.WriteAllLinesAsync(filePath, lines: fileContent.ToArray());
    }

    /// <summary>
    /// A snippet counts as injected when its first non-blank line is already in the file, so generating again (e.g. with
    /// <c>--force</c>) does not add the same registration, seed or endpoint twice. The first line is used instead of the
    /// whole snippet because later lines may differ between runs, such as newly generated Guid ids.
    /// </summary>
    private static bool isAlreadyInjected(IEnumerable<string> fileContent, IEnumerable<string> codeLines)
    {
        string? firstCodeLine = codeLines
            .SelectMany(line => line.Split('\n'))
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
        return firstCodeLine is not null && fileContent.Any(line => line.Trim() == firstCodeLine);
    }
}
