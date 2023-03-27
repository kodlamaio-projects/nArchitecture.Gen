using System.Text.RegularExpressions;
using Application.Features.Generate.Constants;
using Core.CodeGen.File;
using Core.CrossCuttingConcerns.Exceptions;
using Domain.Constants;

namespace Application.Features.Generate.Rules;

public class GenerateBusinessRules
{
    public async Task EntityClassShouldBeInhreitEntityBaseClass(
        string projectPath,
        string entityName
    )
    {
        string[] fileContent = await File.ReadAllLinesAsync(
            $"{projectPath}/Domain/Entities/{entityName}.cs"
        );

        string entityBaseClassNameSpaceUsingTemplate = await File.ReadAllTextAsync(
            $"{DirectoryHelper.AssemblyDirectory}/{Templates.Paths.Crud}/Lines/EntityBaseClassNameSpaceUsing.cs.sbn"
        );
        Regex entityBaseClassRegex = new(@$"public\s+class\s+{entityName}\s*:\s*Entity\s*");
        bool isExists =
            fileContent.Any(line => line == entityBaseClassNameSpaceUsingTemplate)
            && fileContent.Any(entityBaseClassRegex.IsMatch);

        if (!isExists)
            throw new BusinessException(
                GenerateBusinessMessages.EntityClassShouldBeInheritEntityBaseClass(entityName)
            );
    }

    public Task FileShouldNotBeExists(string filePath)
    {
        if (Directory.Exists(filePath))
            throw new BusinessException(GenerateBusinessMessages.FileAlreadyExists(filePath));
        return Task.CompletedTask;
    }
}
