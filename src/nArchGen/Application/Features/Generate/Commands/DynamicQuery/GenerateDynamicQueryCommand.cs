using System.Runtime.CompilerServices;
using Application.Features.Generate.Rules;
using Core.CodeGen.Code;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;

namespace Application.Features.Generate.Commands.DynamicQuery;

public class GenerateDynamicQueryCommand : IStreamRequest<GeneratedDynamicQueryResponse>
{
    public required string ProjectPath { get; set; }
    public required DynamicQueryTemplateData DynamicQueryTemplateData { get; set; }
    public bool Force { get; set; }

    public class GenerateDynamicQueryCommandHandler : IStreamRequestHandler<GenerateDynamicQueryCommand, GeneratedDynamicQueryResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public GenerateDynamicQueryCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules)
        {
            _templateEngine = templateEngine;
            _businessRules = businessRules;
        }

        public async IAsyncEnumerable<GeneratedDynamicQueryResponse> Handle(
            GenerateDynamicQueryCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await _businessRules.FilesShouldNotExistUnlessForced(
                await generateDynamicQueryCodes(request.ProjectPath, request.DynamicQueryTemplateData, pathsOnly: true),
                request.Force
            );

            GeneratedDynamicQueryResponse response = new();
            List<string> newFilePaths = [];

            response.CurrentStatusMessage = "Generating Dynamic Query codes...";
            yield return response;
            newFilePaths.AddRange(await generateDynamicQueryCodes(request.ProjectPath, request.DynamicQueryTemplateData));
            response.LastOperationMessage = "Dynamic Query codes have been generated.";

            string? updatedControllerPath = await injectWebApiEndpoint(request.ProjectPath, request.DynamicQueryTemplateData);
            if (updatedControllerPath is not null)
            {
                response.LastOperationMessage = "GetListByDynamic endpoint has been added to the controller.";
                response.UpdatedFilePathsResult = [updatedControllerPath];
            }

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            yield return response;
        }

        private async Task<string?> injectWebApiEndpoint(string projectPath, DynamicQueryTemplateData dynamicQueryTemplateData)
        {
            string controllerName = $"{dynamicQueryTemplateData.Entity.Name.ToPascalCase().ToPlural()}Controller";
            string controllerFilePath = PlatformHelper.SecuredPathJoin(projectPath, "WebAPI", "Controllers", $"{controllerName}.cs");
            if (!File.Exists(controllerFilePath))
                return null;

            string[] endpointTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.DynamicQuery,
                    "Lines",
                    "DynamicQueryControllerEndpoint.cs.sbn"
                )
            );
            string[] endpointRenderedCodeLines = await Task.WhenAll(
                endpointTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, dynamicQueryTemplateData))
            );
            await CSharpCodeInjector.AddMethodToClass(controllerFilePath, controllerName, endpointRenderedCodeLines);

            string[] usingTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.DynamicQuery,
                    "Lines",
                    "DynamicQueryUsingNameSpaces.cs.sbn"
                )
            );
            string[] usingRenderedCodeLines = await Task.WhenAll(
                usingTemplateCodeLines.Select(async line => await _templateEngine.RenderAsync(line, dynamicQueryTemplateData))
            );
            await CSharpCodeInjector.AddUsingToFile(controllerFilePath, usingRenderedCodeLines);

            return controllerFilePath;
        }

        private async Task<ICollection<string>> generateDynamicQueryCodes(
            string projectPath,
            DynamicQueryTemplateData dynamicQueryTemplateData,
            bool pathsOnly = false
        )
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.DynamicQuery,
                "Folders",
                "Application"
            );
            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Application"),
                dynamicQueryTemplateData,
                pathsOnly
            );
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            DynamicQueryTemplateData dynamicQueryTemplateData,
            bool pathsOnly = false
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(templateDir, searchPattern: $"*.{_templateEngine.TemplateExtension}")
                .ToList();
            Dictionary<string, string> replacePathVariable = new()
            {
                { "PLURAL_ENTITY", "{{ entity.name | string.pascalcase | string.plural }}" },
                { "ENTITY", "{{ entity.name | string.pascalcase }}" },
            };
            ICollection<string> newRenderedFilePaths = pathsOnly
                ? await _templateEngine.RenderFilePathsAsync(
                    templateFilePaths,
                    templateDir,
                    replacePathVariable,
                    outputDir,
                    dynamicQueryTemplateData
                )
                : await _templateEngine.RenderFileAsync(
                    templateFilePaths,
                    templateDir,
                    replacePathVariable,
                    outputDir,
                    dynamicQueryTemplateData
                );
            return newRenderedFilePaths;
        }
    }
}
