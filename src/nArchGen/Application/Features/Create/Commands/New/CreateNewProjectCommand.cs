using System.Runtime.CompilerServices;
using Core.CodeGen.Code;
using Core.CodeGen.CommandLine.Git;
using Core.CodeGen.File;
using MediatR;

namespace Application.Features.Create.Commands.New;

public class CreateNewProjectCommand : IStreamRequest<CreatedNewProjectResponse>
{
    public string ProjectName { get; set; }

    public class CreateNewProjectCommandHandler
        : IStreamRequestHandler<CreateNewProjectCommand, CreatedNewProjectResponse>
    {
        public async IAsyncEnumerable<CreatedNewProjectResponse> Handle(
            CreateNewProjectCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            CreatedNewProjectResponse response = new();
            List<string> newFilePaths = new();

            response.CurrentStatusMessage = "Cloning starter project and core packages...";
            yield return response;
            response.OutputMessage = null;
            await cloneCorePackagesAndStarterProject(request.ProjectName);
            response.LastOperationMessage =
                "Starter project has been cloned from 'https://github.com/kodlamaio-projects/nArchitecture'.";

            response.CurrentStatusMessage = "Renaming project...";
            yield return response;
            await renameProject(request.ProjectName);
            response.LastOperationMessage =
                $"Project has been renamed with {request.ProjectName.ToPascalCase()}.";
            DirectoryHelper.DeleteDirectory(
                $"{Environment.CurrentDirectory}/{request.ProjectName}/.git"
            );
            ICollection<string> newFiles = DirectoryHelper.GetFilesInDirectoryTree(
                root: $"{Environment.CurrentDirectory}/{request.ProjectName}",
                searchPattern: "*"
            );

            response.CurrentStatusMessage = "Initializing git repository with submodules...";
            yield return response;
            await initializeGitRepository(request.ProjectName);
            response.LastOperationMessage = "Git repository has been initialized.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFiles;
            response.OutputMessage =
                $":warning: Check the configuration that has name 'appsettings.json' in 'src/{request.ProjectName.ToCamelCase()}'.";
            response.OutputMessage =
                ":warning: Run 'Update-Database' nuget command on the Persistence layer to apply initial migration.";
            yield return response;
        }

        private async Task cloneCorePackagesAndStarterProject(string projectName)
        {
            await GitCommandHelper.RunAsync(
                $"clone https://github.com/kodlamaio-projects/nArchitecture.git ./{projectName}"
            );
        }

        private async Task renameProject(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/NArchitecture.sln",
                search: "NArchitecture",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/NArchitecture.sln.DotSettings",
                search: "NArchitecture",
                projectName: projectName.ToPascalCase()
            );

            string projectPath = $"{Environment.CurrentDirectory}/src/{projectName.ToCamelCase()}";
            Directory.Move(
                sourceDirName: $"{Environment.CurrentDirectory}/src/starterProject",
                projectPath
            );

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}.sln",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/tests/Application.Tests/Application.Tests.csproj",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            await replaceFileContentWithProjectName(
                path: $"{projectPath}/WebAPI/appsettings.json",
                search: "StarterProject",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{projectPath}/WebAPI/appsettings.json",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            Directory.SetCurrentDirectory("../");

            async Task replaceFileContentWithProjectName(
                string path,
                string search,
                string projectName
            )
            {
                if (path.Contains(search))
                {
                    string newPath = path.Replace(search, projectName);
                    Directory.Move(path, newPath);
                    path = newPath;
                }

                string fileContent = await File.ReadAllTextAsync(path);
                fileContent = fileContent.Replace(search, projectName);
                await File.WriteAllTextAsync(path, fileContent);
            }
        }

        private async Task initializeGitRepository(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");
            await GitCommandHelper.RunAsync($"init");
            await GitCommandHelper.RunAsync($"branch -m master main");
            Directory.Delete($"{Environment.CurrentDirectory}/src/corePackages/");
            await GitCommandHelper.RunAsync(
                "submodule add https://github.com/kodlamaio-projects/nArchitecture.Core ./src/corePackages"
            );
            await GitCommandHelper.CommitChangesAsync(
                "chore: initial commit from nArchitecture.Gen"
            );
            Directory.SetCurrentDirectory("../");
        }
    }
}
