﻿using System.Runtime.CompilerServices;
using NArchitecture.Gen.Application.Features.CodeGeneration.Rules;
using Core.CodeGen.Code;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using NArchitecture.Gen.Domain.Shared.Constants;
using NArchitecture.Gen.Domain.Features.CodeGeneration.ValueObjects;
using MediatR;

namespace NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Query;

public class GenerateQueryCommand : IStreamRequest<GeneratedQueryResponse>
{
    public string QueryName { get; set; } = null!;
    public string FeatureName { get; set; } = null!;
    public string ProjectPath { get; set; } = null!;
    public QueryTemplateData QueryTemplateData { get; set; } = null!;

    public class GenerateQueryCommandHandler : IStreamRequestHandler<GenerateQueryCommand, GeneratedQueryResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public GenerateQueryCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules)
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
                PlatformHelper.SecuredPathJoin(
                    request.ProjectPath,
                    "Application",
                    "features",
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
            updatedFilePaths.AddRange(await injectOperationClaims(request.ProjectPath, request.FeatureName, request.QueryTemplateData));
            response.LastOperationMessage = "Application layer codes have been generated.";

            response.CurrentStatusMessage = "Adding endpoint to WebAPI...";
            yield return response;
            updatedFilePaths.AddRange(await injectWebApiEndpoint(request.ProjectPath, request.FeatureName, request.QueryTemplateData));
            response.LastOperationMessage = $"New endpoint has been add to {request.FeatureName.ToPascalCase()}Controller.";

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
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Application"),
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
                "Application",
                "Features",
                featureName,
                "Constants",
                $"{featureName}OperationClaims.cs"
            );

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
                "Persistence",
                "EntityConfigurations",
                "OperationClaimConfiguration.cs"
            );

            if (!File.Exists(operationClaimsEntityConfigurationFilePath))
                return new[] { featureOperationClaimFilePath };

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
            return new[] { featureOperationClaimFilePath, operationClaimsEntityConfigurationFilePath };
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
            Dictionary<string, string> replacePathVariable =
                new() { { "FEATURE", "{{ feature_name | string.pascalcase }}" }, { "QUERY", "{{ query_name | string.pascalcase }}" } };
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
            string controllerFilePath = PlatformHelper.SecuredPathJoin(projectPath, "WebAPI", "Controllers", $"{featureName}Controller.cs");
            string[] controllerEndPointMethodTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Query,
                    "Lines",
                    "ControllerEndPointMethod.cs.sbn"
                )
            );
            string[] controllerEndPointMethodRenderedCodeLines = await Task.WhenAll(
                controllerEndPointMethodTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, QueryTemplateData))
            );

            await CSharpCodeInjector.AddMethodToClass(
                controllerFilePath,
                className: $"{featureName.ToPascalCase()}Controller",
                controllerEndPointMethodRenderedCodeLines
            );

            string[] queryUsingNameSpaceTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Query,
                    "Lines",
                    "QueryUsingNameSpaces.cs.sbn"
                )
            );
            string[] queryUsingNameSpaceCodeLines = await Task.WhenAll(
                queryUsingNameSpaceTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, QueryTemplateData))
            );
            await CSharpCodeInjector.AddUsingToFile(controllerFilePath, queryUsingNameSpaceCodeLines);

            return new[] { controllerFilePath };
        }
    }
}
