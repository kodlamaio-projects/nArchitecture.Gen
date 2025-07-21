﻿using System.Runtime.CompilerServices;
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

    public class GenerateCommandCommandHandler : IStreamRequestHandler<GenerateCommandCommand, GeneratedCommandResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public GenerateCommandCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules)
        {
            _templateEngine = templateEngine;
            _businessRules = businessRules;
        }

        public async IAsyncEnumerable<GeneratedCommandResponse> Handle(
            GenerateCommandCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await _businessRules.FileShouldNotBeExists(
                PlatformHelper.SecuredPathJoin(
                    request.ProjectPath,
                    "Application",
                    "features",
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

            response.CurrentStatusMessage = "Adding endpoint to WebAPI...";
            yield return response;
            updatedFilePaths.AddRange(await injectWebApiEndpoint(request.ProjectPath, request.FeatureName, request.CommandTemplateData));
            response.LastOperationMessage = $"New endpoint has been add to {request.FeatureName.ToPascalCase()}Controller.";

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
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Application"),
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
                "Application",
                "Features",
                featureName,
                "Constants",
                $"{featureName}OperationClaims.cs"
            );
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
                "Persistence",
                "EntityConfigurations",
                "OperationClaimConfiguration.cs"
            );

            if (!File.Exists(operationClaimsEntityConfigurationFilePath))
                return new[] { featureOperationClaimFilePath };

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

            return new[] { featureOperationClaimFilePath, operationClaimsEntityConfigurationFilePath };
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
            Dictionary<string, string> replacePathVariable =
                new() { { "FEATURE", "{{ feature_name | string.pascalcase }}" }, { "COMMAND", "{{ command_name | string.pascalcase }}" } };
            ICollection<string> newRenderedFilePaths = await _templateEngine.RenderFileAsync(
                templateFilePaths,
                templateDir,
                replacePathVariable,
                outputDir,
                commandTemplateData
            );
            return newRenderedFilePaths;
        }

        private async Task<ICollection<string>> injectWebApiEndpoint(
            string projectPath,
            string featureName,
            CommandTemplateData commandTemplateData
        )
        {
            string controllerFilePath = PlatformHelper.SecuredPathJoin(projectPath, "WebAPI", "Controllers", $"{featureName}Controller.cs");

            string[] controllerEndPointMethodTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Lines",
                    "ControllerEndPointMethod.cs.sbn"
                )
            );
            string[] controllerEndPointMethodRenderedCodeLines = await Task.WhenAll(
                controllerEndPointMethodTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, commandTemplateData))
            );

            await CSharpCodeInjector.AddMethodToClass(
                controllerFilePath,
                className: $"{featureName.ToPascalCase()}Controller",
                controllerEndPointMethodRenderedCodeLines
            );

            string[] commandUsingNameSpaceTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Command,
                    "Lines",
                    "CommandUsingNameSpaces.cs.sbn"
                )
            );
            string[] commandUsingNameSpaceRenderedCodeLines = await Task.WhenAll(
                commandUsingNameSpaceTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, commandTemplateData))
            );
            await CSharpCodeInjector.AddUsingToFile(controllerFilePath, commandUsingNameSpaceRenderedCodeLines);

            return new[] { controllerFilePath };
        }
    }
}
