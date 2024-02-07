using Application.Features.Generate.Rules;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;
using System.Runtime.CompilerServices;

namespace Application.Features.Generate.Commands.Crud;

public class GenerateCrudCommand : IStreamRequest<GeneratedCrudResponse>
{
    public string ProjectPath { get; set; }
    public CrudTemplateData CrudTemplateData { get; set; }
    public string DbContextName { get; set; }

    public class GenerateCrudCommandHandler : IStreamRequestHandler<GenerateCrudCommand, GeneratedCrudResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public GenerateCrudCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules)
        {
            _templateEngine = templateEngine;
            _businessRules = businessRules;
        }

        public async IAsyncEnumerable<GeneratedCrudResponse> Handle(
            GenerateCrudCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await _businessRules.EntityClassShouldBeInhreitEntityBaseClass(request.ProjectPath, request.CrudTemplateData.Entity.Name);

            GeneratedCrudResponse response = new();
            List<string> newFilePaths = new();
            List<string> updatedFilePaths = new();

            response.CurrentStatusMessage = $"Adding {request.CrudTemplateData.Entity.Name} entity to BaseContext.";
            yield return response;
            updatedFilePaths.Add(await injectEntityToContext(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = $"{request.CrudTemplateData.Entity.Name} has been added to BaseContext.";

            response.CurrentStatusMessage = "Generating Persistence layer codes...";
            yield return response;
            newFilePaths.AddRange(await generatePersistenceCodes(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = "Persistence layer codes have been generated.";

            response.CurrentStatusMessage = "Generating Application layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateApplicationCodes(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = "Application layer codes have been generated.";

            response.CurrentStatusMessage = "Adding service registrations...";
            yield return response;
            updatedFilePaths.AddRange(await injectServiceRegistrations(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = "Service registrations have been added.";

            response.CurrentStatusMessage = "Generating WebAPI layer codes...";
            yield return response;
            newFilePaths.AddRange(await generateWebApiCodes(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = "WebAPI layer codes have been generated.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.UpdatedFilePathsResult = updatedFilePaths;
            yield return response;
        }

        private async Task<string> injectEntityToContext(string projectPath, CrudTemplateData crudTemplateData)
        {
            string contextFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "Persistence",
                "Contexts",
                $"{crudTemplateData.DbContextName}.cs"
            );

            string[] entityNameSpaceUsingTemplate = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityNameSpaceUsing.cs.sbn"
                )
            );
            await CSharpCodeInjector.AddUsingToFile(contextFilePath, entityNameSpaceUsingTemplate);

            string dbSetPropertyTemplateCodeLine = await File.ReadAllTextAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityContextProperty.cs.sbn"
                )
            );
            string dbSetPropertyCodeLine = await _templateEngine.RenderAsync(dbSetPropertyTemplateCodeLine, crudTemplateData);
            bool isExists = (await File.ReadAllLinesAsync(contextFilePath)).Any(line => line.Contains(dbSetPropertyCodeLine));
            if (!isExists)
                await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(contextFilePath, codeLines: new[] { dbSetPropertyCodeLine });

            return contextFilePath;
        }

        private async Task<ICollection<string>> generatePersistenceCodes(string projectPath, CrudTemplateData crudTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Crud,
                "Folders",
                "Persistence"
            );
            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Persistence"),
                crudTemplateData
            );
        }

        private async Task<ICollection<string>> generateApplicationCodes(string projectPath, CrudTemplateData crudTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Crud,
                "Folders",
                "Application"
            );

            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Application"),
                crudTemplateData
            );
        }

        private async Task<ICollection<string>> generateWebApiCodes(string projectPath, CrudTemplateData crudTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.Crud,
                "Folders",
                "WebAPI"
            );
            return await generateFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "WebAPI"),
                crudTemplateData
            );
        }

        private async Task<ICollection<string>> generateFolderCodes(string templateDir, string outputDir, CrudTemplateData crudTemplateData)
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
                crudTemplateData
            );
            return newRenderedFilePaths;
        }

        private async Task<ICollection<string>> injectServiceRegistrations(string projectPath, CrudTemplateData crudTemplateData)
        {
            #region Persistence
            string persistenceServiceRegistrationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "Persistence",
                "PersistenceServiceRegistration.cs"
            );

            string persistenceServiceRegistrationTemplateCodeLine = await File.ReadAllTextAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityRepositoryServiceRegistration.cs.sbn"
                )
            );
            string persistenceServiceRegistrationRenderedCodeLine = await _templateEngine.RenderAsync(
                persistenceServiceRegistrationTemplateCodeLine,
                crudTemplateData
            );
            await CSharpCodeInjector.AddUsingToFile(
                persistenceServiceRegistrationFilePath,
                usingLines: new[] { "using Application.Services.Repositories;", "using Persistence.Repositories;" }
            );
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                persistenceServiceRegistrationFilePath,
                methodName: "AddPersistenceServices",
                codeLines: new[] { persistenceServiceRegistrationRenderedCodeLine }
            );
            #endregion

            #region Application
            string applicationServiceRegistrationNameSpaceUsingFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "Application",
                "ApplicationServiceRegistration.cs"
            );

            string applicationServiceRegistrationNameSpaceUsingTemplateCodeLine = await File.ReadAllTextAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityServiceRegistrationNameSpaceUsing.cs.sbn"
                )
            );
            string applicationServiceRegistrationNameSpaceUsingRenderedCodeLine = await _templateEngine.RenderAsync(
                applicationServiceRegistrationNameSpaceUsingTemplateCodeLine,
                crudTemplateData
            );
            await CSharpCodeInjector.AddUsingToFile(
                applicationServiceRegistrationNameSpaceUsingFilePath,
                usingLines: new[] { applicationServiceRegistrationNameSpaceUsingRenderedCodeLine }
            );

            string applicationServiceRegistrationFilePath = PlatformHelper.SecuredPathJoin(
                projectPath,
                "Application",
                "ApplicationServiceRegistration.cs"
            );

            string applicationServiceRegistrationTemplateCodeLine = await File.ReadAllTextAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityServiceRegistration.cs.sbn"
                )
            );
            string applicationServiceRegistrationRenderedCodeLine = await _templateEngine.RenderAsync(
                applicationServiceRegistrationTemplateCodeLine,
                crudTemplateData
            );
            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                applicationServiceRegistrationFilePath,
                methodName: "AddApplicationServices",
                codeLines: new[] { applicationServiceRegistrationRenderedCodeLine }
            );
            #endregion

            return new[] { persistenceServiceRegistrationFilePath, applicationServiceRegistrationFilePath };
        }
    }
}
