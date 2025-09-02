using System.Runtime.CompilerServices;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Generate.Commands.DynamicQuery;

public class GenerateDynamicQueryCommand : IStreamRequest<GeneratedDynamicQueryResponse>
{
    public required string ProjectPath { get; set; }
    public required DynamicQueryTemplateData DynamicQueryTemplateData { get; set; }

    public class GenerateDynamicQueryCommandHandler : IStreamRequestHandler<GenerateDynamicQueryCommand, GeneratedDynamicQueryResponse>
    {
        private readonly ITemplateEngine _templateEngine;

        public GenerateDynamicQueryCommandHandler(ITemplateEngine templateEngine)
        {
            _templateEngine = templateEngine;
        }

        public async IAsyncEnumerable<GeneratedDynamicQueryResponse> Handle(
            GenerateDynamicQueryCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            GeneratedDynamicQueryResponse response = new();
            List<string> newFilePaths = [];

            response.CurrentStatusMessage = "Generating Dynamic Query codes...";
            yield return response;
            newFilePaths.AddRange(await generateDynamicQueryCodes(request.ProjectPath, request.DynamicQueryTemplateData));
            response.LastOperationMessage = "Dynamic Query codes have been generated.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            yield return response;
        }

        private async Task<ICollection<string>> generateDynamicQueryCodes(
            string projectPath,
            DynamicQueryTemplateData dynamicQueryTemplateData
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
                dynamicQueryTemplateData
            );
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            DynamicQueryTemplateData dynamicQueryTemplateData
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(templateDir, searchPattern: $"*.{_templateEngine.TemplateExtension}")
                .ToList();
            Dictionary<string, string> replacePathVariable =
                new()
                {
                    { "PLURAL_ENTITY", "{{ entity.name | string.pascalcase | string.plural }}" },
                    { "ENTITY", "{{ entity.name | string.pascalcase }}" }
                };
            ICollection<string> newRenderedFilePaths = await _templateEngine.RenderFileAsync(
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
