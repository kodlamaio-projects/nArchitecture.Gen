using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;
using System.Runtime.CompilerServices;

namespace Application.Features.Generate.Commands.Crud;

public class GenerateCrudCommand : IStreamRequest<GeneratedCrudResponse>
{
    public CrudTemplateData CrudTemplateData { get; set; }
    public string DbContextName { get; set; }

    public class GenerateCrudCommandHandler
        : IStreamRequestHandler<GenerateCrudCommand, GeneratedCrudResponse>
    {
        private readonly ITemplateEngine _templateEngine;

        public GenerateCrudCommandHandler(ITemplateEngine templateEngine)
        {
            _templateEngine = templateEngine;
        }

        public async IAsyncEnumerable<GeneratedCrudResponse> Handle(
            GenerateCrudCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            GeneratedCrudResponse response = new();
            List<string> newFilePaths = new();
            List<string> updatedFilePaths = new();

            response.CurrentStatusMessage =
                $"Adding {request.CrudTemplateData.Entity.Name} entity to BaseContext.";
            yield return response;
            updatedFilePaths.Add(await injectEntityToContext(request.CrudTemplateData));
            response.LastOperationMessage =
                $"{request.CrudTemplateData.Entity.Name} has been added to BaseContext.";

            response.CurrentStatusMessage = "Generating Persistence layer codes...";
            yield return response;
            newFilePaths.AddRange(await generatePersistenceCodes(request.CrudTemplateData));
            response.LastOperationMessage = "Persistence layer codes have been generated.";

            response.CurrentStatusMessage = "Adding feature operation claims as seed...";
            yield return response;
            updatedFilePaths.Add(await injectFeatureOperationClaims(request.CrudTemplateData));
            response.LastOperationMessage = "Feature operation claims have been added.";

            response.CurrentStatusMessage = "Generating Application layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateApplicationCodes(request.CrudTemplateData));
            response.LastOperationMessage = "Application layer codes have been generated.";

            response.CurrentStatusMessage = "Adding service registrations...";
            yield return response;
            updatedFilePaths.AddRange(await injectServiceRegistrations(request.CrudTemplateData));
            response.LastOperationMessage = "Service registrations have been added.";

            response.CurrentStatusMessage = "Generating WebAPI layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateWebApiCodes(request.CrudTemplateData));
            response.LastOperationMessage = "WebAPI layer codes have been generated.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.UpdatedFilePathsResult = updatedFilePaths;
            yield return response;
        }

        private async Task<string> injectEntityToContext(CrudTemplateData crudTemplateData)
        {
            string contextFilePath =
                @$"{Environment.CurrentDirectory}\Persistence\Contexts\{crudTemplateData.DbContextName}.cs";
            string dbSetPropertyTemplateCodeLine = await File.ReadAllTextAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Lines\EntityBaseContextProperty.cs.sbn"
            );
            string dbSetPropertyCodeLine = await _templateEngine.RenderAsync(
                dbSetPropertyTemplateCodeLine,
                crudTemplateData
            );

            await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(
                contextFilePath,
                codeLines: new[] { dbSetPropertyCodeLine }
            );
            return contextFilePath;
        }

        private async Task<ICollection<string>> generatePersistenceCodes(
            CrudTemplateData crudTemplateData
        )
        {
            string templateDir =
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Folders\Persistence";
            return await generateFolderCodes(
                templateDir,
                outputDir: $@"{Environment.CurrentDirectory}\Persistence",
                crudTemplateData
            );
        }

        private async Task<string> injectFeatureOperationClaims(CrudTemplateData crudTemplateData)
        {
            string operationClaimConfigurationFilePath =
                @$"{Environment.CurrentDirectory}\Persistence\EntityConfigurations\OperationClaimConfiguration.cs";
            string[] seedTemplateCodeLines = await File.ReadAllLinesAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Lines\EntityFeatureOperationClaimSeeds.cs.sbn"
            );

            List<string> seedCodeLines = new() { string.Empty };
            foreach (string templateCodeLine in seedTemplateCodeLines)
            {
                string seedCodeLine = await _templateEngine.RenderAsync(
                    templateCodeLine,
                    crudTemplateData
                );
                seedCodeLines.Add(seedCodeLine);
            }
            seedCodeLines.Add(string.Empty);

            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                operationClaimConfigurationFilePath,
                methodName: "getSeeds",
                codeLines: seedCodeLines.ToArray()
            );
            return operationClaimConfigurationFilePath;
        }

        private async Task<ICollection<string>> generateApplicationCodes(
            CrudTemplateData crudTemplateData
        )
        {
            string templateDir =
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Folders\Application";
            return await generateFolderCodes(
                templateDir,
                outputDir: $@"{Environment.CurrentDirectory}\Application",
                crudTemplateData
            );
        }

        private async Task<ICollection<string>> generateWebApiCodes(
            CrudTemplateData crudTemplateData
        )
        {
            string templateDir =
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Folders\WebAPI";
            return await generateFolderCodes(
                templateDir,
                outputDir: $@"{Environment.CurrentDirectory}\WebAPI",
                crudTemplateData
            );
        }

        private async Task<ICollection<string>> generateFolderCodes(
            string templateDir,
            string outputDir,
            CrudTemplateData crudTemplateData
        )
        {
            var templateFilePaths = DirectoryHelper
                .GetFilesInDirectoryTree(
                    templateDir,
                    searchPattern: $"*.{_templateEngine.TemplateExtension}"
                )
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
                crudTemplateData
            );
            return newRenderedFilePaths;
        }

        private async Task<ICollection<string>> injectServiceRegistrations(
            CrudTemplateData crudTemplateData
        )
        {
            string persistenceServiceRegistrationFilePath =
                @$"{Environment.CurrentDirectory}\Persistence\PersistenceServiceRegistration.cs";
            string persistenceServiceRegistrationTemplateCodeLine = await File.ReadAllTextAsync(
                @$"{DirectoryHelper.AssemblyDirectory}\{Templates.Paths.Crud}\Lines\EntityRepositoryServiceRegistration.cs.sbn"
            );
            string persistenceServiceRegistrationRenderedCodeLine =
                await _templateEngine.RenderAsync(
                    persistenceServiceRegistrationTemplateCodeLine,
                    crudTemplateData
                );

            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                persistenceServiceRegistrationFilePath,
                methodName: "AddPersistenceServices",
                codeLines: new[] { persistenceServiceRegistrationRenderedCodeLine }
            );
            return new[] { persistenceServiceRegistrationFilePath };
        }
    }
}
