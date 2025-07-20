using System.IO.Compression;
using System.Runtime.CompilerServices;
using Core.CodeGen.Code;
using Core.CodeGen.CommandLine.Git;
using Core.CodeGen.File;
using MediatR;
using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;

namespace NArchitecture.Gen.Application.Features.ProjectManagement.Commands.New;

public class CreateNewProjectCommand : IStreamRequest<CreatedNewProjectResponse>
{
    public string ProjectName { get; set; }
    public string? TemplateId { get; set; }

    public CreateNewProjectCommand()
    {
        ProjectName = string.Empty;
    }

    public CreateNewProjectCommand(string projectName, string? templateId = null)
    {
        ProjectName = projectName;
        TemplateId = templateId;
    }


    public class CreateNewProjectCommandHandler : IStreamRequestHandler<CreateNewProjectCommand, CreatedNewProjectResponse>
    {
        private readonly ITemplateService _templateService;

        public CreateNewProjectCommandHandler(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        public async IAsyncEnumerable<CreatedNewProjectResponse> Handle(
            CreateNewProjectCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            CreatedNewProjectResponse response = new();
            List<string> newFilePaths = [];

            // Resolve template
            ProjectTemplate template = string.IsNullOrEmpty(request.TemplateId)
                ? await _templateService.GetDefaultTemplateAsync()
                : await _templateService.GetTemplateByIdAsync(request.TemplateId);

            response.CurrentStatusMessage = $"Downloading template '{template.Name}'...";
            yield return response;
            response.OutputMessage = null;
            await downloadTemplateProject(request.ProjectName, template);
            response.LastOperationMessage = $"Template '{template.Name}' has been downloaded from '{template.RepositoryUrl}'.";

            response.CurrentStatusMessage = "Preparing project...";
            yield return response;
            await renameProject(request.ProjectName);
            response.LastOperationMessage = $"Project has been prepared with {request.ProjectName.ToPascalCase()}.";

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
                $":warning: Check the configuration that has name 'appsettings.json' in 'src/{request.ProjectName.ToCamelCase()}/WebAPI'.";
            yield return response;
        }

        private async Task downloadTemplateProject(string projectName, ProjectTemplate template)
        {
            string downloadUrl = await buildDownloadUrl(template);
            
            using HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(300); // 5 minutes timeout
            using HttpResponseMessage response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            
            string zipPath = $"{Environment.CurrentDirectory}/{projectName}.zip";
            await using Stream zipStream = await response.Content.ReadAsStreamAsync();
            await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write);
            await zipStream.CopyToAsync(fileStream);
            fileStream.Close();
            
            ZipFile.ExtractToDirectory(zipPath, Environment.CurrentDirectory);
            File.Delete(zipPath);
            
            // Find the extracted directory and rename it
            string extractedDirName = await findExtractedDirectory(template, downloadUrl);
            Directory.Move(
                sourceDirName: $"{Environment.CurrentDirectory}/{extractedDirName}",
                $"{Environment.CurrentDirectory}/{projectName}"
            );
        }

        private async Task<string> buildDownloadUrl(ProjectTemplate template)
        {
            TemplateConfiguration config = await _templateService.GetTemplateConfigurationAsync();
            string version = await _templateService.ResolveTemplateVersionAsync(template);
            
            if (config.Settings.IsDebugMode || template.InstallationMode == TemplateInstallationMode.Branch)
            {
                // For debug mode or branch-based templates, use branch download
                string branchName = template.BranchName ?? "main";
                return $"{template.RepositoryUrl}/archive/refs/heads/{branchName}.zip";
            }
            else
            {
                // For production mode, use release download
                return $"{template.RepositoryUrl}/archive/refs/tags/v{version}.zip";
            }
        }

        private async Task<string> findExtractedDirectory(ProjectTemplate template, string downloadUrl)
        {
            // Extract repository name and version/branch from URL to determine extracted folder name
            Uri uri = new Uri(template.RepositoryUrl);
            string repoName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            
            if (downloadUrl.Contains("/archive/refs/tags/"))
            {
                // Release download: repoName-version
                string version = await _templateService.ResolveTemplateVersionAsync(template);
                return $"{repoName}-{version}";
            }
            else
            {
                // Branch download: repoName-branchName
                string branchName = template.BranchName ?? "main";
                return $"{repoName}-{branchName}";
            }
        }

        private async Task renameProject(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");

            // Templates now use .slnx format
            string solutionFile = "NArchitecture.Starter.slnx";
            
            if (File.Exists($"{Environment.CurrentDirectory}/{solutionFile}"))
            {
                await replaceFileContentWithProjectName(
                    path: $"{Environment.CurrentDirectory}/{solutionFile}",
                    search: solutionFile.Contains("Starter") ? "NArchitecture.Starter" : "NArchitecture",
                    projectName: projectName.ToPascalCase()
                );
                
                // Rename solution file to match project name
                string newSolutionFile = $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}.slnx";
                if (solutionFile != $"{projectName.ToPascalCase()}.slnx")
                {
                    File.Move($"{Environment.CurrentDirectory}/{solutionFile}", newSolutionFile);
                }
            }

            // Handle .DotSettings file if it exists
            string dotSettingsFile = "NArchitecture.Starter.slnx.DotSettings";
            if (File.Exists($"{Environment.CurrentDirectory}/{dotSettingsFile}"))
            {
                await replaceFileContentWithProjectName(
                    path: $"{Environment.CurrentDirectory}/{dotSettingsFile}",
                    search: "NArchitecture.Starter",
                    projectName: projectName.ToPascalCase()
                );
            }

            // Handle source project directory structure
            string[] srcDirectories = Directory.GetDirectories($"{Environment.CurrentDirectory}/src");
            if (srcDirectories.Length > 0)
            {
                string sourceProjectDir = srcDirectories.First().Split('/').Last();
                string newProjectPath = $"{Environment.CurrentDirectory}/src/{projectName.ToCamelCase()}";
                Directory.Move(sourceDirName: $"{Environment.CurrentDirectory}/src/{sourceProjectDir}", newProjectPath);

                // Update all project files in the source directory
                await updateProjectFilesRecursively(newProjectPath, sourceProjectDir, projectName);
            }

            // Handle test project directory structure
            string[] testDirectories = Directory.GetDirectories($"{Environment.CurrentDirectory}/tests");
            if (testDirectories.Length > 0)
            {
                string sourceTestDir = testDirectories.First().Split('/').Last();
                string newTestPath = $"{Environment.CurrentDirectory}/tests/{projectName.ToPascalCase()}";
                Directory.Move(sourceDirName: $"{Environment.CurrentDirectory}/tests/{sourceTestDir}", newTestPath);

                // Update all test project files
                await updateProjectFilesRecursively(newTestPath, sourceTestDir, projectName);
            }

            Directory.SetCurrentDirectory("../");

            async Task updateProjectFilesRecursively(string directoryPath, string originalName, string newProjectName)
            {
                // Update all .csproj files and rename them
                string[] projectFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
                foreach (string projectFile in projectFiles)
                {
                    await replaceFileContentWithProjectName(
                        path: projectFile,
                        search: "NArchitecture.Starter",
                        projectName: newProjectName.ToPascalCase()
                    );
                    
                    // Rename the project file itself
                    if (Path.GetFileName(projectFile).Contains("NArchitecture.Starter"))
                    {
                        string newFileName = Path.GetFileName(projectFile).Replace("NArchitecture.Starter", newProjectName.ToPascalCase());
                        string newFilePath = Path.Combine(Path.GetDirectoryName(projectFile)!, newFileName);
                        File.Move(projectFile, newFilePath);
                    }
                }

                // Update all .cs files
                string[] csFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
                foreach (string csFile in csFiles)
                {
                    await replaceFileContentWithProjectName(
                        path: csFile,
                        search: "NArchitecture.Starter",
                        projectName: newProjectName.ToPascalCase()
                    );
                }

                // Update appsettings.json if it exists
                string[] jsonFiles = Directory.GetFiles(directoryPath, "appsettings*.json", SearchOption.AllDirectories);
                foreach (string jsonFile in jsonFiles)
                {
                    await replaceFileContentWithProjectName(
                        path: jsonFile,
                        search: "NArchitecture.Starter",
                        projectName: newProjectName.ToPascalCase()
                    );
                }
            }


            static async Task replaceFileContentWithProjectName(string path, string search, string projectName)
            {
                if (!File.Exists(path)) return;

                string fileContent = await File.ReadAllTextAsync(path);
                if (fileContent.Contains(search))
                {
                    fileContent = fileContent.Replace(search, projectName);
                    await File.WriteAllTextAsync(path, fileContent);
                }
            }
        }


        private async Task initializeGitRepository(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");
            await GitCommandHelper.RunAsync($"init");
            await GitCommandHelper.RunAsync($"branch -m master main");
            await GitCommandHelper.CommitChangesAsync("chore: initial commit from NArchitecture.Gen");
            Directory.SetCurrentDirectory("../");
        }
    }
}
