using System.Runtime.CompilerServices;
using Application.Features.Generate.Rules;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Generate.Commands.Crud;

public class GenerateCrudCommand : IStreamRequest<GeneratedCrudResponse>
{
    public required string ProjectPath { get; set; }
    public required CrudTemplateData CrudTemplateData { get; set; }
    public required string DbContextName { get; set; }

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
            List<string> newFilePaths = [];
            List<string> updatedFilePaths = [];

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

            if (request.CrudTemplateData.IsDynamicQueryUsed)
            {
                response.CurrentStatusMessage = "Generating Dynamic Query codes...";
                yield return response;
                newFilePaths.AddRange(await generateDynamicQueryCodes(request.ProjectPath, request.CrudTemplateData));
                response.LastOperationMessage = "Dynamic Query codes have been generated.";
            }

            response.CurrentStatusMessage = "Adding operation claims as seed data...";
            yield return response;
            updatedFilePaths.Add(await injectOperationClaims(request.ProjectPath, request.CrudTemplateData));
            response.LastOperationMessage = "Operation claims have been added.";

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

        private async Task<string> injectOperationClaims(string projectPath, CrudTemplateData crudTemplateData)
        {
            string operationClaimConfigurationFilePath;

            if (!string.IsNullOrEmpty(crudTemplateData.CustomOperationClaimPath))
            {
                operationClaimConfigurationFilePath = crudTemplateData.CustomOperationClaimPath;
            }
            else
            {
                operationClaimConfigurationFilePath = PlatformHelper.SecuredPathJoin(
                    projectPath,
                    "Persistence",
                    "EntityConfigurations",
                    "OperationClaimConfiguration.cs"
                );
            }

            if (!File.Exists(operationClaimConfigurationFilePath))
                return $"Not Found: {operationClaimConfigurationFilePath}";

            string[] seedTemplateCodeLines = await File.ReadAllLinesAsync(
                PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityFeatureOperationClaimSeeds.cs.sbn"
                )
            );

            List<string> seedCodeLines = new() { string.Empty };
            foreach (string templateCodeLine in seedTemplateCodeLines)
            {
                string seedCodeLine = await _templateEngine.RenderAsync(templateCodeLine, crudTemplateData);
                seedCodeLines.Add(seedCodeLine);
            }
            seedCodeLines.Add(string.Empty);

            await CSharpCodeInjector.AddCodeLinesToMethodAsync(
                operationClaimConfigurationFilePath,
                methodName: "getFeatureOperationClaims",
                codeLines: seedCodeLines.ToArray()
            );

            // Using statement'ı sadece custom path kullanılmıyorsa ekle
            if (!crudTemplateData.IsCustomOperationClaimPath)
            {
                string featureOperationClaimUsingTemplatePath = PlatformHelper.SecuredPathJoin(
                    DirectoryHelper.AssemblyDirectory,
                    Templates.Paths.Crud,
                    "Lines",
                    "EntityFeatureOperationClaimsNameSpaceUsing.cs.sbn"
                );
                string featureOperationClaimUsingTemplate = await File.ReadAllTextAsync(featureOperationClaimUsingTemplatePath);
                string featureOperationClaimUsingRendered = await _templateEngine.RenderAsync(
                    featureOperationClaimUsingTemplate,
                    crudTemplateData
                );
                await CSharpCodeInjector.AddUsingToFile(
                    operationClaimConfigurationFilePath,
                    usingLines: featureOperationClaimUsingRendered.Split(Environment.NewLine)
                );
            }
            return operationClaimConfigurationFilePath;
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
                await CSharpCodeInjector.AddCodeLinesAsPropertyAsync(contextFilePath, codeLines: [dbSetPropertyCodeLine]);

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

        private async Task<ICollection<string>> generateDynamicQueryCodes(string projectPath, CrudTemplateData crudTemplateData)
        {
            string templateDir = PlatformHelper.SecuredPathJoin(
                DirectoryHelper.AssemblyDirectory,
                Templates.Paths.DynamicQuery,
                "Folders",
                "Application"
            );

            DynamicQueryTemplateData dynamicQueryTemplateData =
                new()
                {
                    Entity = crudTemplateData.Entity,
                    IsCachingUsed = crudTemplateData.IsCachingUsed,
                    IsLoggingUsed = crudTemplateData.IsLoggingUsed,
                    IsSecuredOperationUsed = crudTemplateData.IsSecuredOperationUsed
                };

            return await generateDynamicQueryFolderCodes(
                templateDir,
                outputDir: PlatformHelper.SecuredPathJoin(projectPath, "Application"),
                dynamicQueryTemplateData
            );
        }

        private async Task<ICollection<string>> generateDynamicQueryFolderCodes(
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
