using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;

namespace NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;

public interface ITemplateService
{
    Task<TemplateConfiguration> GetTemplateConfigurationAsync();
    Task<List<ProjectTemplate>> GetAvailableTemplatesAsync();
    Task<ProjectTemplate> GetTemplateByIdAsync(string templateId);
    Task<ProjectTemplate> GetDefaultTemplateAsync();
    Task<string> ResolveTemplateVersionAsync(ProjectTemplate template, string? requestedVersion = null);
}