using System.Runtime.CompilerServices;
using NArchitecture.Gen.Application.Features.CodeGeneration.Rules;
using Core.CodeGen.Code;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using NArchitecture.Gen.Domain.Shared.Constants;
using NArchitecture.Gen.Domain.Features.CodeGeneration.ValueObjects;
using NArchitecture.Core.Mediator.Abstractions;

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Command;

public class GenerateCommandCommand : IStreamRequest<GeneratedCommandResponse>
{
    public string CommandName { get; set; } = null!;
    public string FeatureName { get; set; } = null!;
    public string ProjectPath { get; set; } = null!;
    public CommandTemplateData CommandTemplateData { get; set; } = null!;

    public class GenerateCommandCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules) : IStreamRequestHandler<GenerateCommandCommand, GeneratedCommandResponse>
    {
        private readonly ITemplateEngine _templateEngine = templateEngine;
        private readonly GenerateBusinessRules _businessRules = businessRules;

        public async IAsyncEnumerable<GeneratedCommandResponse> Handle(
            GenerateCommandCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await _businessRules.FileShouldNotBeExists(
                PlatformHelper.SecuredPathJoin(
                    request.ProjectPath,
                    "core",
                    "Application",
                    "Features",
                    request.FeatureName.ToPascalCase(),
                    "Commands",
                    request.CommandName,
                    $"{request.CommandName}Command.cs"
                )
            );

            GeneratedCommandResponse response = new();
            List<string> newFilePaths = [];
            List<string> updatedFilePaths = [];

            response.CurrentStatusMessage = "Generating Application layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateApplicationCodes(request.ProjectPath, request.CommandTemplateData));
            updatedFilePaths.AddRange(await injectOperationClaims(request.ProjectPath, request.FeatureName, request.CommandTemplateData));
            response.LastOperationMessage = "Application layer codes have been generated.";

            if (request.CommandTemplateData.IsApiEndpointUsed)
            {
                response.CurrentStatusMessage = "Generating WebAPI endpoint...";
                yield return response;
                newFilePaths.AddRange(await generateWebApiEndpoint(request.ProjectPath, request.FeatureName, request.CommandTemplateData));
                updatedFilePaths.AddRange(await injectWebApiEndpoint(request.ProjectPath, request.FeatureName, request.CommandTemplateData));
                response.LastOperationMessage = $"New endpoint has been generated for {request.FeatureName.ToPascalCase()}.";
            }
            else
            {
                response.LastOperationMessage = "API endpoint generation skipped.";
            }

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.UpdatedFilePathsResult = updatedFilePaths;
            yield return response;
        }

        private async Task<ICollection<string>> generateApplicationCodes(string projectPath, CommandTemplateData commandTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Command,
                "Folders",
                "Application"
            );
            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "core", "Application"),
                commandTemplateData
            );
        }

        private async Task<ICollection<string>> injectOperationClaims(
            string projectPath,
            string featureName,
            CommandTemplateData commandTemplateData
        )
        {
            string featureOperationClaimFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "core",
                "Application",
                "Features",
                featureName,
                "Constants",
                $"{featureName}OperationClaims.cs"
            );

            // Check if the operation claims file exists, if not create it
            if (!File.Exists(featureOperationClaimFilePath))
            {
                await createOperationClaimsFile(featureOperationClaimFilePath, commandTemplateData);
            }

            string[] commandOperationClaimPropertyTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Lines",
                    "CommandOperationClaimProperty.cs.sbn"
                )
            );
            string[] commandOperationClaimPropertyCodeLines = await Task.WhenAll(
                commandOperationClaimPropertyTemplateCodeLines.Select(async line =>
                    await _templateEngine.RenderAsync(line, commandTemplateData)
                )
            );
            await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(featureOperationClaimFilePath, commandOperationClaimPropertyCodeLines);

            string operationClaimsEntityConfigurationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "infrastructure",
                "Persistence",
                "Features",
                "OperationClaimConfiguration.cs"
            );

            if (!File.Exists(operationClaimsEntityConfigurationFilePath))
                return [featureOperationClaimFilePath];

            string[] commandOperationClaimSeedTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Lines",
                    "CommandOperationClaimSeed.cs.sbn"
                )
            );
            string[] commandOperationClaimSeedCodeLines = await Task.WhenAll(
                commandOperationClaimSeedTemplateCodeLines.Select(async line =>
                    await _templateEngine.RenderAsync(line, commandTemplateData)
                )
            );
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                operationClaimsEntityConfigurationFilePath,
                "getFeatureOperationClaims",
                commandOperationClaimSeedCodeLines
            );

            return [featureOperationClaimFilePath, operationClaimsEntityConfigurationFilePath];
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            CommandTemplateData commandTemplateData
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(templateDir, searchPattern: $"*.{_templateEngine.TemplateExtension}")
                .ToList();

            // Filter out operation claims template if security is not enabled
            if (!commandTemplateData.IsSecuredOperationUsed)
            {
                templateFilePaths = templateFilePaths
                    .Where(static path => !path.Contains("FEATUREOperationClaims.cs.sbn"))
                    .ToList();
            }

            Dictionary<string, string> replacePathVariable =
                new() { { "FEATURE", "{{ feature_name | string.pascalcase }}" }, { "COMMAND", "{{ command_name | string.pascalcase }}" } };
            return await _templateEngine.RenderFileAsync(
                templateFilePaths,
                templateDir,
                replacePathVariable,
                outputDir,
                commandTemplateData
            );
        }

        private async Task<ICollection<string>> generateWebApiEndpoint(string projectPath, string featureName, CommandTemplateData commandTemplateData)
        {
            List<string> newFiles = [];

            // Check if feature registration file already exists
            string featureRegistrationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "presentation",
                "WebApi",
                "Features",
                featureName.ToPascalCase(),
                $"{featureName.ToPascalCase()}EndpointRegistration.cs"
            );

            if (File.Exists(featureRegistrationFilePath))
            {
                // Only generate the endpoint file if registration already exists
                string endpointTemplateFilePath = PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Folders",
                    "WebApi",
                    "Features",
                    "FEATURE",
                    "Endpoints",
                    "COMMANDEndpoint.cs.sbn"
                );

                string endpointOutputFilePath = PlatformHelper.SecuredPathJoin(
                    projectPath,
                    "presentation",
                    "WebApi",
                    "Features",
                    featureName.ToPascalCase(),
                    "Endpoints",
                    $"{commandTemplateData.CommandName.ToPascalCase()}Endpoint.cs"
                );

                // Ensure the directory exists
                string? endpointOutputDir = Path.GetDirectoryName(endpointOutputFilePath);
                if (endpointOutputDir != null && !Directory.Exists(endpointOutputDir))
                {
                    _ = Directory.CreateDirectory(endpointOutputDir);
                }

                // Read and render the template
                string templateContent = await File.ReadAllTextAsync(endpointTemplateFilePath);
                string renderedContent = await _templateEngine.RenderAsync(templateContent, commandTemplateData);

                // Write the rendered content to the file
                await File.WriteAllTextAsync(endpointOutputFilePath, renderedContent);
                newFiles.Add(endpointOutputFilePath);
            }
            else
            {
                // Generate both endpoint and registration files
                string templateDir = PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Folders",
                    "WebApi"
                );

                newFiles.AddRange(await generateFolderCodes(
                    templateDir,
                    outputDir: PlatformHelper.SecuredPathJoin(projectPath, "presentation", "WebApi"),
                    commandTemplateData
                ));
            }

            return newFiles;
        }

        private async Task<ICollection<string>> injectWebApiEndpoint(
            string projectPath,
            string featureName,
            CommandTemplateData commandTemplateData
        )
        {
            List<string> updatedFiles = [];

            // Path to the feature endpoint registration file
            string featureRegistrationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "presentation",
                "WebApi",
                "Features",
                featureName.ToPascalCase(),
                $"{featureName.ToPascalCase()}EndpointRegistration.cs"
            );

            // If the feature registration file exists, inject the endpoint mapping
            if (File.Exists(featureRegistrationFilePath))
            {
                // Check if the endpoint mapping already exists to avoid duplicates
                string fileContent = await File.ReadAllTextAsync(featureRegistrationFilePath);

                // Read endpoint mapping template to generate expected endpoint call
                string[] endpointMappingTemplateCodeLines = await File.ReadAllLinesAsync(
                    PlatformHelper.SecuredPathJoin(
                        DirectoryHelper.AssemblyDirectory,
                        Templates.Paths.Command,
                        "Lines",
                        "EndpointMapping.cs.sbn"
                    )
                );

                // Generate expected endpoint call from template to avoid magic strings
                string expectedEndpointCall = await _templateEngine.RenderAsync(endpointMappingTemplateCodeLines[0], commandTemplateData);

                if (!fileContent.Contains(expectedEndpointCall))
                {
                    string[] endpointMappingRenderedCodeLines = await Task.WhenAll(
                        endpointMappingTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, commandTemplateData))
                    );

                    // Find the method that registers endpoints and add the new mapping
                    await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                        featureRegistrationFilePath,
                        $"Map{featureName.ToPascalCase()}Endpoints",
                        endpointMappingRenderedCodeLines
                    );

                    // Add using statement for the endpoint
                    string[] endpointUsingNameSpaceTemplateCodeLines = await File.ReadAllLinesAsync(
                        PlatformHelper.SecuredPathJoin(
                            DirectoryHelper.AssemblyDirectory,
                            Templates.Paths.Command,
                            "Lines",
                            "EndpointUsingNameSpaces.cs.sbn"
                        )
                    );
                    string[] endpointUsingNameSpaceRenderedCodeLines = await Task.WhenAll(
                        endpointUsingNameSpaceTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, commandTemplateData))
                    );
                    await CSharpCodeInjector.AddUsingToFile(featureRegistrationFilePath, endpointUsingNameSpaceRenderedCodeLines);

                    updatedFiles.Add(featureRegistrationFilePath);
                }
            }

            return updatedFiles;
        }

        private async Task createOperationClaimsFile(string filePath, CommandTemplateData commandTemplateData)
        {
            string templateFilePath = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Command,
                "Folders",
                "Application",
                "Features",
                "FEATURE",
                "Constants",
                "FEATUREOperationClaims.cs.sbn"
            );

            // Ensure the directory exists
            string? directoryPath = Path.GetDirectoryName(filePath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
            {
                _ = Directory.CreateDirectory(directoryPath);
            }

            // Read and render the template
            string templateContent = await File.ReadAllTextAsync(templateFilePath);
            string renderedContent = await _templateEngine.RenderAsync(templateContent, commandTemplateData);

            // Write the rendered content to the file
            await File.WriteAllTextAsync(filePath, renderedContent);
        }
    }
}
