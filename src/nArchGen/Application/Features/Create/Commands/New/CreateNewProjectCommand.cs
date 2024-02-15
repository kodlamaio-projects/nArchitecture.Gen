using System.IO.Compression;
using System.Runtime.CompilerServices;
using Core.CodeGen.Code;
using Core.CodeGen.CommandLine.Git;
using Core.CodeGen.File;
using MediatR;

namespace Application.Features.Create.Commands.New;

public class CreateNewProjectCommand : IStreamRequest<CreatedNewProjectResponse>
{
    public string ProjectName { get; set; }
    public bool IsThereSecurityMechanism { get; set; } = true;

    public CreateNewProjectCommand()
    {
        ProjectName = string.Empty;
    }

    public CreateNewProjectCommand(string projectName, bool isThereSecurityMechanism)
    {
        ProjectName = projectName;
        IsThereSecurityMechanism = isThereSecurityMechanism;
    }

    public class CreateNewProjectCommandHandler : IStreamRequestHandler<CreateNewProjectCommand, CreatedNewProjectResponse>
    {
        public async IAsyncEnumerable<CreatedNewProjectResponse> Handle(
            CreateNewProjectCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            CreatedNewProjectResponse response = new();
            List<string> newFilePaths = [];

            response.CurrentStatusMessage = "Cloning starter project and core packages...";
            yield return response;
            response.OutputMessage = null;
            await downloadStarterProject(request.ProjectName);
            response.LastOperationMessage = "Starter project has been cloned from 'https://github.com/kodlamaio-projects/nArchitecture'.";

            response.CurrentStatusMessage = "Preparing project...";
            yield return response;
            await renameProject(request.ProjectName);
            if (!request.IsThereSecurityMechanism)
                await removeSecurityMechanism(request.ProjectName);
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

        private async Task downloadStarterProject(string projectName)
        {
            // Download zip on url
            string releaseUrl = "https://github.com/kodlamaio-projects/nArchitecture/archive/refs/tags/v1.1.0.zip";
            using HttpClient client = new();
            using HttpResponseMessage response = await client.GetAsync(releaseUrl);
            response.EnsureSuccessStatusCode();
            string zipPath = $"{Environment.CurrentDirectory}/{projectName}.zip";
            await using Stream zipStream = await response.Content.ReadAsStreamAsync();
            await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write);
            await zipStream.CopyToAsync(fileStream);
            fileStream.Close();
            ZipFile.ExtractToDirectory(zipPath, Environment.CurrentDirectory);
            File.Delete(zipPath);
            Directory.Move(
                sourceDirName: $"{Environment.CurrentDirectory}/nArchitecture-1.1.0",
                $"{Environment.CurrentDirectory}/{projectName}"
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
            Directory.Move(sourceDirName: $"{Environment.CurrentDirectory}/src/starterProject", projectPath);

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}.sln",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            string testProjectDir = $"{Environment.CurrentDirectory}/tests/{projectName.ToPascalCase()}.Application.Tests";
            Directory.Move(
                sourceDirName: $"{Environment.CurrentDirectory}/tests/StarterProject.Application.Tests/",
                destDirName: testProjectDir
            );
            await replaceFileContentWithProjectName(
                path: $"{testProjectDir}/StarterProject.Application.Tests.csproj",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{testProjectDir}/StarterProject.Application.Tests.csproj",
                search: "StarterProject",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}.sln",
                search: "StarterProject",
                projectName: projectName.ToPascalCase()
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

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.development.yml",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.development.yml",
                search: "NArchitecture",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.staging.yml",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.staging.yml",
                search: "NArchitecture",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.production.yml",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/.azure/azure-pipelines.production.yml",
                search: "NArchitecture",
                projectName: projectName.ToPascalCase()
            );

            Directory.SetCurrentDirectory("../");

            static async Task replaceFileContentWithProjectName(string path, string search, string projectName)
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

        private async Task removeSecurityMechanism(string projectName)
        {
            string slnPath = $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}";
            string projectSourcePath = $"{slnPath}/src/{projectName.ToCamelCase()}";
            string projectTestsPath = $"{slnPath}/tests/";

            string[] dirsToDelete = new[]
            {
                $"{projectSourcePath}/Application/Features/Auth",
                $"{projectSourcePath}/Application/Features/OperationClaims",
                $"{projectSourcePath}/Application/Features/UserOperationClaims",
                $"{projectSourcePath}/Application/Features/Users",
                $"{projectSourcePath}/Application/Services/AuthenticatorService",
                $"{projectSourcePath}/Application/Services/AuthService",
                $"{projectSourcePath}/Application/Services/OperationClaims",
                $"{projectSourcePath}/Application/Services/UserOperationClaims",
                $"{projectSourcePath}/Application/Services/UsersService",
                $"{projectSourcePath}/Domain/Entities",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Features/Auth",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Features/Users",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/Repositories/Auth",
            };
            foreach (string dirPath in dirsToDelete)
                Directory.Delete(dirPath, recursive: true);

            string[] filesToDelete = new[]
            {
                $"{projectSourcePath}/Application/Services/Repositories/IEmailAuthenticatorRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IOperationClaimRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IOtpAuthenticatorRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IRefreshTokenRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IUserOperationClaimRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IUserRepository.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/EmailAuthenticatorConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/OperationClaimConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/OtpAuthenticatorConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/RefreshTokenConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/UserConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/UserOperationClaimConfiguration.cs",
                $"{projectSourcePath}/Persistence/Repositories/EmailAuthenticatorRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/OperationClaimRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/OtpAuthenticatorRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/RefreshTokenRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/UserOperationClaimRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/UserRepository.cs",
                $"{projectSourcePath}/WebAPI/Controllers/AuthController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/OperationClaimsController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/UserOperationClaimsController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/UsersController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/Dtos/UpdateByAuthFromServiceRequestDto.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/DependencyResolvers/AuthServiceRegistrations.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/DependencyResolvers/UsersTestServiceRegistration.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/FakeDatas/OperationClaimFakeData.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/FakeDatas/RefreshTokenFakeData.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/FakeDatas/UserFakeData.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/Repositories/UserMockRepository.cs",
                $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Mocks/Configurations/MockConfiguration.cs",
            };
            foreach (string filePath in filesToDelete)
                File.Delete(filePath);

            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Application/ApplicationServiceRegistration.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Application.Services.AuthenticatorService;",
                            "using Application.Services.AuthService;",
                            "using Application.Services.UsersService;",
                            "services.AddScoped<IAuthService, AuthManager>();",
                            "services.AddScoped<IAuthenticatorService, AuthenticatorManager>();",
                            "services.AddScoped<IUserService, UserManager>();",
                            "using NArchitecture.Core.Security.DependencyInjection;",
                            "services.AddSecurityServices<Guid, int>();",
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Application/Application.csproj",
                predicate: line =>
                    (new[] { "<PackageReference Include=\"NArchitecture.Core.Security.DependencyInjection\" Version=\"1.0.0\" />", }).Any(
                        line.Contains
                    )
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Domain/Domain.csproj",
                predicate: line =>
                    (new[] { "<PackageReference Include=\"NArchitecture.Core.Security\" Version=\"1.1.1\" />" }).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Persistence/Contexts/BaseDbContext.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Domain.Entities;",
                            "DbSet<EmailAuthenticator> EmailAuthenticators",
                            "DbSet<OperationClaim> OperationClaim",
                            "DbSet<OtpAuthenticator> OtpAuthenticator",
                            "DbSet<RefreshToken> RefreshTokens",
                            "DbSet<User> User",
                            "DbSet<UserOperationClaim> UserOperationClaims",
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Persistence/PersistenceServiceRegistration.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Persistence.Repositories;",
                            "using Application.Services.Repositories;",
                            "services.AddScoped<IEmailAuthenticatorRepository, EmailAuthenticatorRepository>()",
                            "services.AddScoped<IOperationClaimRepository, OperationClaimRepository>()",
                            "services.AddScoped<IOtpAuthenticatorRepository, OtpAuthenticatorRepository>();",
                            "services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()",
                            "services.AddScoped<IUserRepository, UserRepository>();",
                            "services.AddScoped<IUserOperationClaimRepository, UserOperationClaimRepository>();",
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectTestsPath}/{projectName.ToPascalCase()}.Application.Tests/Startup.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Application.Services.AuthenticatorService;",
                            "using Application.Services.AuthService;",
                            $"using StarterProject.Application.Tests.DependencyResolvers;",
                            "using Core.Security;",
                            "services.AddUsersServices();",
                            "services.AddAuthServices();",
                        }
                    ).Any(line.Contains)
            );

            await FileHelper.RemoveContentAsync(
                filePath: $"{projectSourcePath}/WebAPI/Program.cs",
                contents: new[]
                {
                    "using Core.Security;\n",
                    "using Core.Security.Encryption;\n",
                    "using Core.Security.JWT;\n",
                    "using Core.WebAPI.Extensions.Swagger;\n",
                    "using Microsoft.AspNetCore.Authentication.JwtBearer;\n",
                    "using Microsoft.IdentityModel.Tokens;\n",
                    "using Microsoft.OpenApi.Models;\n",
                    "const string tokenOptionsConfigurationSection = \"TokenOptions\";\nTokenOptions tokenOptions =\n    builder.Configuration.GetSection(tokenOptionsConfigurationSection).Get<TokenOptions>()\n    ?? throw new InvalidOperationException($\"\\\"{tokenOptionsConfigurationSection}\\\" section cannot found in configuration.\");\nbuilder\n    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)\n    .AddJwtBearer(options =>\n    {\n        options.TokenValidationParameters = new TokenValidationParameters\n        {\n            ValidateIssuer = true,\n            ValidateAudience = true,\n            ValidateLifetime = true,\n            ValidIssuer = tokenOptions.Issuer,\n            ValidAudience = tokenOptions.Audience,\n            ValidateIssuerSigningKey = true,\n            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey)\n        };\n    });\n\n",
                    "    opt.AddSecurityDefinition(\n        name: \"Bearer\",\n        securityScheme: new OpenApiSecurityScheme\n        {\n            Name = \"Authorization\",\n            Type = SecuritySchemeType.Http,\n            Scheme = \"Bearer\",\n            BearerFormat = \"JWT\",\n            In = ParameterLocation.Header,\n            Description =\n                \"JWT Authorization header using the Bearer scheme. Example: \\\"Authorization: Bearer YOUR_TOKEN\\\". \\r\\n\\r\\n\"\n                + \"`Enter your token in the text input below.`\"\n        }\n    );\n    opt.OperationFilter<BearerSecurityRequirementOperationFilter>();\n",
                    "app.UseAuthentication();\n",
                    "app.UseAuthorization();\n"
                }
            );
            await FileHelper.RemoveContentAsync(
                filePath: $"{projectSourcePath}/WebAPI/WebAPI.csproj",
                contents: new[] { "<PackageReference Include=\"NArchitecture.Core.Security.WebApi.Swagger\" Version=\"1.0.0\" />;", }
            );
        }

        private async Task initializeGitRepository(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");
            await GitCommandHelper.RunAsync($"init");
            await GitCommandHelper.RunAsync($"branch -m master main");
            await GitCommandHelper.CommitChangesAsync("chore: initial commit from nArchitecture.Gen");
            Directory.SetCurrentDirectory("../");
        }
    }
}
