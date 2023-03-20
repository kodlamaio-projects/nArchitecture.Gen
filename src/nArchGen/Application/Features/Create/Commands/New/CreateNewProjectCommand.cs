using System.Runtime.CompilerServices;
using Core.CodeGen.Code;
using Core.CodeGen.CommandLine.Git;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Create.Commands.New;

public class CreateNewProjectCommand : IStreamRequest<CreatedNewProjectResponse>
{
    public NewProjectData NewProjectData { get; set; }

    public class CreateNewProjectCommandHandler
        : IStreamRequestHandler<CreateNewProjectCommand, CreatedNewProjectResponse>
    {
        ITemplateEngine _templateEngine;

        public CreateNewProjectCommandHandler(ITemplateEngine templateEngine)
        {
            _templateEngine = templateEngine;
        }

        public async IAsyncEnumerable<CreatedNewProjectResponse> Handle(
            CreateNewProjectCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            CreatedNewProjectResponse response = new();
            List<string> newFilePaths = new();

            response.OutputMessage =
                "Starter Project Repository: https://github.com/kodlamaio-projects/nArchitecture.RentACarProject";
            response.CurrentStatusMessage = "Cloning starter project and core packages...";
            yield return response;
            response.OutputMessage = null;
            newFilePaths.AddRange(
                await generateFolderCodes(
                    templateDir: @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.NewProject}\",
                    request.NewProjectData
                )
            );
            response.LastOperationMessage = "Starter project has been cloned.";

            response.CurrentStatusMessage = "Initializing git repository...";
            yield return response;
            await initializeGitRepository();
            response.LastOperationMessage = "Git repository has been initialized.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.OutputMessage =
                "Run 'Add-Migration Initial' and 'Update-Database' commands on Persistence layer.";
            yield return response;
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            NewProjectData newProjectData
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(templateDir, searchPattern: $"*")
                .ToList();
            Dictionary<string, string> replacePathVariable =
                new()
                {
                    { "PROJECT_NAME_C", "{{ project_name | string.camelcase }}" },
                    { "PROJECT_NAME", "{{ project_name | string.pascalcase }}" }
                };
            ICollection<string> newRenderedFilePaths = await _templateEngine.RenderFileAsync(
                templateFilePaths,
                templateDir,
                replacePathVariable,
                outputDir: $"{Environment.CurrentDirectory}/",
                newProjectData
            );
            return newRenderedFilePaths;
        }

        private async Task initializeGitRepository()
        {
            await GitCommandHelper.RunAsync("init");
            await GitCommandHelper.RunAsync("branch -m master main");
            await GitCommandHelper.CommitChangesAsync(
                "chore: initial commit from nArchitecture-cli"
            );
        }
    }
}
