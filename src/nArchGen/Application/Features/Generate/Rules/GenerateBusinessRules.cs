using System.Text.RegularExpressions;
using Application.Features.Generate.Constants;
using Core.CodeGen.File;
using Core.CrossCuttingConcerns.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;

namespace Application.Features.Generate.Rules;

public class GenerateBusinessRules
{
    public async Task EntityClassShouldBeInhreitEntityBaseClass(string projectPath, string entityName)
    {
        string[] fileContent = await File.ReadAllLinesAsync(
            PlatformHelper.SecuredPathJoin(projectPath, "Domain", "Entities", $"{entityName}.cs")
        );

        string entityBaseClassNameSpaceUsingTemplate = await File.ReadAllTextAsync(
            PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Crud,
                "Lines",
                "EntityBaseClassNameSpaceUsing.cs.sbn"
            )
        );
        Regex entityBaseClassRegex = new(@$"public\s+class\s+{entityName}\s*:\s*Entity\s*");
        bool isExists =
            fileContent.Any(line => line == entityBaseClassNameSpaceUsingTemplate) && fileContent.Any(entityBaseClassRegex.IsMatch);

        if (!isExists)
            throw new BusinessException(GenerateBusinessMessages.EntityClassShouldBeInheritEntityBaseClass(entityName));
    }

    /// <summary>Fails before anything is written when any of the files to be generated already exists, unless overwriting is allowed.</summary>
    public Task FilesShouldNotExistUnlessForced(IEnumerable<string> filePaths, bool force)
    {
        if (force)
            return Task.CompletedTask;

        string[] existingFilePaths = filePaths.Where(File.Exists).ToArray();
        if (existingFilePaths.Length > 0)
            throw new ConflictException(GenerateBusinessMessages.FilesAlreadyExist(existingFilePaths.Length), existingFilePaths);
        return Task.CompletedTask;
    }
}
