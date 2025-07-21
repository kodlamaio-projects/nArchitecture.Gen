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

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Query;

public class GenerateQueryCommand : IStreamRequest<GeneratedQueryResponse>
{
    public string QueryName { get; set; } = null!;
    public string FeatureName { get; set; } = null!;
    public string ProjectPath { get; set; } = null!;
    public QueryTemplateData QueryTemplateData { get; set; } = null!;

    public class GenerateQueryCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules) : IStreamRequestHandler<GenerateQueryCommand, GeneratedQueryResponse>
    {
        private readonly ITemplateEngine _templateEngine = templateEngine;
        private readonly GenerateBusinessRules _businessRules = businessRules;

        public async IAsyncEnumerable<GeneratedQueryResponse> Handle(
            GenerateQueryCommand request,
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
                    "Queries",
                    request.QueryName,
                    $"{request.QueryName}Query.cs"
                )
            );

            GeneratedQueryResponse response = new();
            List<string> newFilePaths = [];
            List<string> updatedFilePaths = [];

            response.CurrentStatusMessage = "Generating Application layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateApplicationCodes(request.ProjectPath, request.QueryTemplateData));

            // Only inject operation claims if security is used
            if (request.QueryTemplateData.IsSecuredOperationUsed)
            {
                updatedFilePaths.AddRange(await injectOperationClaims(request.ProjectPath, request.FeatureName, request.QueryTemplateData));
            }

            response.LastOperationMessage = "Application layer codes have been generated.";

            if (request.QueryTemplateData.IsApiEndpointUsed)
            {
                response.CurrentStatusMessage = "Generating WebAPI endpoint...";
                yield return response;
                newFilePaths.AddRange(await generateWebApiEndpoint(request.ProjectPath, request.FeatureName, request.QueryTemplateData));
                updatedFilePaths.AddRange(await injectWebApiEndpoint(request.ProjectPath, request.FeatureName, request.QueryTemplateData));
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

        private async Task<ICollection<string>> generateApplicationCodes(string projectPath, QueryTemplateData QueryTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Query,
                "Folders",
                "Application"
            );
            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "core", "Application"),
                QueryTemplateData
            );
        }

        private async Task<ICollection<string>> injectOperationClaims(
            string projectPath,
            string featureName,
            QueryTemplateData QueryTemplateData
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
                await createOperationClaimsFile(projectPath, featureName, QueryTemplateData);
            }

            string[] queryOperationClaimPropertyTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Query,
                    "Lines",
                    "QueryOperationClaimProperty.cs.sbn"
                )
            );
            string[] queryOperationClaimPropertyCodeLines = await Task.WhenAll(
                queryOperationClaimPropertyTemplateCodeLines.Select(async line =>
                    await _templateEngine.RenderAsync(line, QueryTemplateData)
                )
            );
            await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(featureOperationClaimFilePath, queryOperationClaimPropertyCodeLines);

            string operationClaimsEntityConfigurationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "infrastructure",
                "Persistence",
                "Features",
                "OperationClaimConfiguration.cs"
            );

            if (!File.Exists(operationClaimsEntityConfigurationFilePath))
                return [featureOperationClaimFilePath];

            string[] queryOperationClaimSeedTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Query,
                    "Lines",
                    "QueryOperationClaimSeed.cs.sbn"
                )
            );
            string[] queryOperationClaimSeedCodeLines = await Task.WhenAll(
                queryOperationClaimSeedTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, QueryTemplateData))
            );
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                operationClaimsEntityConfigurationFilePath,
                "getFeatureOperationClaims",
                queryOperationClaimSeedCodeLines
            );
            return [featureOperationClaimFilePath, operationClaimsEntityConfigurationFilePath];
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            QueryTemplateData QueryTemplateData
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(templateDir, searchPattern: $"*.{_templateEngine.TemplateExtension}")
                .ToList();

            // Filter out operation claims template if security is not enabled
            if (!QueryTemplateData.IsSecuredOperationUsed)
            {
                templateFilePaths = templateFilePaths
                    .Where(static path => !path.Contains("FEATUREOperationClaims.cs.sbn"))
                    .ToList();
            }

            Dictionary<string, string> replacePathVariable =
                new() { { "FEATURE", "{{ feature_name | string.pascalcase }}" }, { "QUERY", "{{ query_name | string.pascalcase }}" } };
            return await _templateEngine.RenderFileAsync(
                templateFilePaths,
                templateDir,
                replacePathVariable,
                outputDir,
                QueryTemplateData
            );
        }

        private async Task<ICollection<string>> generateWebApiEndpoint(string projectPath, string featureName, QueryTemplateData queryTemplateData)
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
                    Templates.Paths.Query,
                    "Folders",
                    "WebApi",
                    "Features",
                    "FEATURE",
                    "Endpoints",
                    "QUERYEndpoint.cs.sbn"
                );

                string endpointOutputFilePath = PlatformHelper.SecuredPathJoin(
                    projectPath,
                    "presentation",
                    "WebApi",
                    "Features",
                    featureName.ToPascalCase(),
                    "Endpoints",
                    $"{queryTemplateData.QueryName.ToPascalCase()}Endpoint.cs"
                );

                // Ensure the directory exists
                string? endpointOutputDir = Path.GetDirectoryName(endpointOutputFilePath);
                if (endpointOutputDir != null && !Directory.Exists(endpointOutputDir))
                {
                    _ = Directory.CreateDirectory(endpointOutputDir);
                }

                // Read and render the template
                string templateContent = await File.ReadAllTextAsync(endpointTemplateFilePath);
                string renderedContent = await _templateEngine.RenderAsync(templateContent, queryTemplateData);

                // Write the rendered content to the file
                await File.WriteAllTextAsync(endpointOutputFilePath, renderedContent);
                newFiles.Add(endpointOutputFilePath);
            }
            else
            {
                // Generate both endpoint and registration files
                string templateDir = PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Query,
                    "Folders",
                    "WebApi"
                );

                newFiles.AddRange(await generateFolderCodes(
                    templateDir,
                    outputDir: PlatformHelper.SecuredPathJoin(projectPath, "presentation", "WebApi"),
                    queryTemplateData
                ));
            }

            return newFiles;
        }

        private async Task<ICollection<string>> injectWebApiEndpoint(
            string projectPath,
            string featureName,
            QueryTemplateData queryTemplateData
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
                // Check if the endpoint mapping already exists to prevent duplicates
                string fileContent = await File.ReadAllTextAsync(featureRegistrationFilePath);

                // Read endpoint mapping template to generate expected endpoint mapping
                string[] endpointMappingTemplateCodeLines = await File.ReadAllLinesAsync(
                    PlatformHelper.SecuredPathJoin(
                        DirectoryHelper.AssemblyDirectory,
                        Templates.Paths.Query,
                        "Lines",
                        "EndpointMapping.cs.sbn"
                    )
                );

                // Generate expected endpoint mapping from template to avoid magic strings
                string expectedEndpointMapping = await _templateEngine.RenderAsync(endpointMappingTemplateCodeLines[0], queryTemplateData);

                if (!fileContent.Contains(expectedEndpointMapping))
                {
                    string[] endpointMappingRenderedCodeLines = await Task.WhenAll(
                        endpointMappingTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, queryTemplateData))
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
                            Templates.Paths.Query,
                            "Lines",
                            "EndpointUsingNameSpaces.cs.sbn"
                        )
                    );
                    string[] endpointUsingNameSpaceRenderedCodeLines = await Task.WhenAll(
                        endpointUsingNameSpaceTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, queryTemplateData))
                    );
                    await CSharpCodeInjector.AddUsingToFile(featureRegistrationFilePath, endpointUsingNameSpaceRenderedCodeLines);

                    updatedFiles.Add(featureRegistrationFilePath);
                }
            }

            return updatedFiles;
        }

        private async Task createOperationClaimsFile(string projectPath, string featureName, QueryTemplateData queryTemplateData)
        {
            string operationClaimsTemplateFilePath = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Query,
                "Folders",
                "Application",
                "Features",
                "FEATURE",
                "Constants",
                "FEATUREOperationClaims.cs.sbn"
            );

            string operationClaimsOutputFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "core",
                "Application",
                "Features",
                featureName,
                "Constants",
                $"{featureName}OperationClaims.cs"
            );

            // Ensure the directory exists
            string? operationClaimsOutputDir = Path.GetDirectoryName(operationClaimsOutputFilePath);
            if (operationClaimsOutputDir != null && !Directory.Exists(operationClaimsOutputDir))
            {
                _ = Directory.CreateDirectory(operationClaimsOutputDir);
            }

            // Read and render the template
            string templateContent = await File.ReadAllTextAsync(operationClaimsTemplateFilePath);
            string renderedContent = await _templateEngine.RenderAsync(templateContent, queryTemplateData);

            // Write the rendered content to the file
            await File.WriteAllTextAsync(operationClaimsOutputFilePath, renderedContent);
        }
    }
}
