using System.Runtime.CompilerServices;
using Application.Features.Generate.Rules;
using Core.CodeGen.Code;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Generate.Commands.Query;

public class GenerateQueryCommand : IStreamRequest<GeneratedQueryResponse>
{
    public string QueryName { get; set; } = null!;
    public string FeatureName { get; set; } = null!;
    public string ProjectPath { get; set; } = null!;
    public QueryTemplateData QueryTemplateData { get; set; } = null!;

    public class GenerateQueryCommandHandler
        : IStreamRequestHandler<GenerateQueryCommand, GeneratedQueryResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public GenerateQueryCommandHandler(
            ITemplateEngine templateEngine,
            GenerateBusinessRules businessRules
        )
        {
            _templateEngine = templateEngine;
            _businessRules = businessRules;
        }

        public async IAsyncEnumerable<GeneratedQueryResponse> Handle(
            GenerateQueryCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await _businessRules.FileShouldNotBeExists(
                @$"{request.ProjectPath}\Application\features\{request.FeatureName.ToPascalCase()}\Queries\{request.QueryName}\{request.QueryName}Query.cs"
            );

            GeneratedQueryResponse response = new();
            List<string> newFilePaths = new();
            List<string> updatedFilePaths = new();

            response.CurrentStatusMessage = "Generating Application layer codes...";
            yield return response;
            newFilePaths.AddRange(
                await generateApplicationCodes(request.ProjectPath, request.QueryTemplateData)
            );
            updatedFilePaths.AddRange(
                await injectOperationClaims(
                    request.ProjectPath,
                    request.FeatureName,
                    request.QueryTemplateData
                )
            );
            response.LastOperationMessage = "Application layer codes have been generated.";

            response.CurrentStatusMessage = "Adding endpoint to WebAPI...";
            yield return response;
            updatedFilePaths.AddRange(
                await injectWebApiEndpoint(
                    request.ProjectPath,
                    request.FeatureName,
                    request.QueryTemplateData
                )
            );
            response.LastOperationMessage =
                $"New endpoint has been add to {request.FeatureName.ToPascalCase()}Controller.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.UpdatedFilePathsResult = updatedFilePaths;
            yield return response;
        }

        private async Task<ICollection<string>> generateApplicationCodes(
            string projectPath,
            QueryTemplateData QueryTemplateData
        )
        {
            string templateDir =
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Query}\Folders\Application";
            return await generateFolderCodes(
                templateDir,
                outputDir: $@"{projectPath}\Application",
                QueryTemplateData
            );
        }

        private async Task<ICollection<string>> injectOperationClaims(
            string projectPath,
            string featureName,
            QueryTemplateData QueryTemplateData
        )
        {
            string featureOperationClaimFilePath =
                @$"{projectPath}\Application\Features\{featureName}\Constants\{featureName}OperationClaims.cs";
            string[] queryOperationClaimPropertyTemplateCodeLines = await File.ReadAllLinesAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Query}\Lines\QueryOperationClaimProperty.cs.sbn"
            );
            string[] queryOperationClaimPropertyCodeLines = await Task.WhenAll(
                queryOperationClaimPropertyTemplateCodeLines.Select(
                    async line => await _templateEngine.RenderAsync(line, QueryTemplateData)
                )
            );
            await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(
                featureOperationClaimFilePath,
                queryOperationClaimPropertyCodeLines
            );

            string operationClaimsEntityConfigurationFilePath =
                @$"{projectPath}\Persistence\EntityConfigurations\OperationClaimConfiguration.cs";
            string[] queryOperationClaimSeedTemplateCodeLines = await File.ReadAllLinesAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Query}\Lines\QueryOperationClaimSeed.cs.sbn"
            );
            string[] queryOperationClaimSeedCodeLines = await Task.WhenAll(
                queryOperationClaimSeedTemplateCodeLines.Select(
                    async line => await _templateEngine.RenderAsync(line, QueryTemplateData)
                )
            );
            await CSharpCodeInjector.AddCodeLinesToRegionAsync(
                operationClaimsEntityConfigurationFilePath,
                queryOperationClaimSeedCodeLines,
                featureName
            );

            return new[]
            {
                featureOperationClaimFilePath,
                operationClaimsEntityConfigurationFilePath
            };
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            QueryTemplateData QueryTemplateData
        )
        {
            List<string> templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(
                    templateDir,
                    searchPattern: $"*.{_templateEngine.TemplateExtension}"
                )
                .ToList();
            Dictionary<string, string> replacePathVariable =
                new()
                {
                    { "FEATURE", "{{ feature_name | string.pascalcase }}" },
                    { "QUERY", "{{ query_name | string.pascalcase }}" }
                };
            ICollection<string> newRenderedFilePaths = await _templateEngine.RenderFileAsync(
                templateFilePaths,
                templateDir,
                replacePathVariable,
                outputDir,
                QueryTemplateData
            );
            return newRenderedFilePaths;
        }

        private async Task<ICollection<string>> injectWebApiEndpoint(
            string projectPath,
            string featureName,
            QueryTemplateData QueryTemplateData
        )
        {
            string controllerFilePath =
                @$"{projectPath}\WebAPI\Controllers\{featureName}Controller.cs";
            string[] controllerEndPointMethodTemplateCodeLines = await File.ReadAllLinesAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Query}\Lines\ControllerEndPointMethod.cs.sbn"
            );
            string[] controllerEndPointMethodRenderedCodeLines = await Task.WhenAll(
                controllerEndPointMethodTemplateCodeLines.Select(
                    async line => await _templateEngine.RenderAsync(line, QueryTemplateData)
                )
            );

            await CSharpCodeInjector.AddMethodToClass(
                controllerFilePath,
                className: $"{featureName.ToPascalCase()}Controller",
                controllerEndPointMethodRenderedCodeLines
            );

            string[] queryUsingNameSpaceTemplateCodeLines = await File.ReadAllLinesAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Query}\Lines\QueryUsingNameSpaces.cs.sbn"
            );
            string[] queryUsingNameSpaceCodeLines = await Task.WhenAll(
                queryUsingNameSpaceTemplateCodeLines.Select(
                    async line => await _templateEngine.RenderAsync(line, QueryTemplateData)
                )
            );
            await CSharpCodeInjector.AddUsingToFile(
                controllerFilePath,
                queryUsingNameSpaceCodeLines
            );

            return new[] { controllerFilePath };
        }
    }
}
