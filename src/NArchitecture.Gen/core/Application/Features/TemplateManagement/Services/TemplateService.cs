using System.Reflection;
using System.Text.Json;
using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;
using Core.CrossCuttingConcerns.Exceptions;

namespace NArchitecture.Gen.Application.Features.TemplateManagement.Services;

/// <summary>
/// Service responsible for managing project templates and their configurations.
///
/// This service loads template configuration from an embedded JSON resource in the Domain layer,
/// providing template selection, configuration management, and version resolution for project creation.
/// The template definitions are maintained as static configuration rather than generated through code,
/// ensuring clean separation of concerns and easier maintenance.
/// </summary>
public class TemplateService : ITemplateService
{
    private const string EmbeddedResourceName = "templates.json";
    private static readonly Assembly DomainAssembly = typeof(TemplateConfiguration).Assembly;
    
    private TemplateConfiguration? _cachedConfiguration;
    private readonly object _cacheLock = new();

    public Task<TemplateConfiguration> GetTemplateConfigurationAsync()
    {
        if (_cachedConfiguration != null)
            return Task.FromResult(_cachedConfiguration);

        lock (_cacheLock)
        {
            if (_cachedConfiguration != null)
                return Task.FromResult(_cachedConfiguration);

            _cachedConfiguration = LoadTemplateConfigurationFromEmbeddedResource();
        }

        return Task.FromResult(_cachedConfiguration);
    }

    public async Task<List<ProjectTemplate>> GetAvailableTemplatesAsync()
    {
        TemplateConfiguration config = await GetTemplateConfigurationAsync();
        return config.Templates;
    }

    public async Task<ProjectTemplate> GetTemplateByIdAsync(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            throw new BusinessException("Template ID cannot be null or empty");

        List<ProjectTemplate> templates = await GetAvailableTemplatesAsync();
        ProjectTemplate? template = templates.FirstOrDefault(t => 
            t.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase));
        
        if (template == null)
            throw new BusinessException($"Template with ID '{templateId}' not found");

        return template;
    }

    public async Task<ProjectTemplate> GetDefaultTemplateAsync()
    {
        TemplateConfiguration config = await GetTemplateConfigurationAsync();
        
        if (string.IsNullOrWhiteSpace(config.Settings.DefaultTemplateId))
            throw new BusinessException("Default template ID is not configured");

        return await GetTemplateByIdAsync(config.Settings.DefaultTemplateId);
    }

    public async Task<string> ResolveTemplateVersionAsync(ProjectTemplate template, string? requestedVersion = null)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        TemplateConfiguration config = await GetTemplateConfigurationAsync();
        
        // In debug mode, use branch name
        if (config.Settings.IsDebugMode)
        {
            return template.BranchName ?? "main";
        }

        // Use requested version if provided
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            return requestedVersion;
        }

        // Use template's release version if available
        if (!string.IsNullOrWhiteSpace(template.ReleaseVersion))
        {
            return template.ReleaseVersion;
        }

        // Fallback to current tool version for compatibility
        return "1.2.2";
    }

    /// <summary>
    /// Loads template configuration from the embedded JSON resource in the Domain assembly.
    /// This method provides robust error handling for resource loading and JSON deserialization.
    /// </summary>
    /// <returns>The loaded template configuration</returns>
    /// <exception cref="BusinessException">Thrown when the resource cannot be found or loaded</exception>
    private TemplateConfiguration LoadTemplateConfigurationFromEmbeddedResource()
    {
        try
        {
            // Get all embedded resource names for debugging
            string[] resourceNames = DomainAssembly.GetManifestResourceNames();
            
            // Find the templates.json resource
            string? resourceName = resourceNames.FirstOrDefault(name => 
                name.EndsWith(EmbeddedResourceName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(resourceName))
            {
                string availableResources = string.Join(", ", resourceNames);
                throw new BusinessException(
                    $"Embedded resource '{EmbeddedResourceName}' not found in Domain assembly. " +
                    $"Available resources: {availableResources}");
            }

            // Load the resource stream
            using Stream? resourceStream = DomainAssembly.GetManifestResourceStream(resourceName);
            
            if (resourceStream == null)
            {
                throw new BusinessException(
                    $"Failed to load embedded resource stream for '{resourceName}'");
            }

            // Read the JSON content
            using var reader = new StreamReader(resourceStream);
            string jsonContent = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                throw new BusinessException(
                    $"Embedded resource '{resourceName}' is empty or contains only whitespace");
            }

            // Deserialize the JSON with proper error handling
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            TemplateConfiguration? configuration = JsonSerializer.Deserialize<TemplateConfiguration>(
                jsonContent, jsonOptions);

            if (configuration == null)
            {
                throw new BusinessException(
                    "Failed to deserialize template configuration: result is null");
            }

            // Validate the loaded configuration
            ValidateTemplateConfiguration(configuration);

            return configuration;
        }
        catch (JsonException ex)
        {
            throw new BusinessException(
                $"Failed to parse template configuration JSON: {ex.Message}", ex);
        }
        catch (BusinessException)
        {
            // Re-throw business exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException(
                $"Unexpected error while loading template configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the loaded template configuration to ensure it contains required data.
    /// </summary>
    /// <param name="configuration">The configuration to validate</param>
    /// <exception cref="BusinessException">Thrown when validation fails</exception>
    private static void ValidateTemplateConfiguration(TemplateConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Version))
        {
            throw new BusinessException("Template configuration version is required");
        }

        if (configuration.Templates == null || configuration.Templates.Count == 0)
        {
            throw new BusinessException("Template configuration must contain at least one template");
        }

        if (configuration.Settings == null)
        {
            throw new BusinessException("Template configuration settings are required");
        }

        if (string.IsNullOrWhiteSpace(configuration.Settings.DefaultTemplateId))
        {
            throw new BusinessException("Default template ID is required in configuration settings");
        }

        // Validate that the default template exists
        bool defaultTemplateExists = configuration.Templates.Any(t => 
            t.Id.Equals(configuration.Settings.DefaultTemplateId, StringComparison.OrdinalIgnoreCase));

        if (!defaultTemplateExists)
        {
            throw new BusinessException(
                $"Default template '{configuration.Settings.DefaultTemplateId}' not found in available templates");
        }

        // Validate individual templates
        foreach (ProjectTemplate template in configuration.Templates)
        {
            ValidateProjectTemplate(template);
        }
    }

    /// <summary>
    /// Validates an individual project template.
    /// </summary>
    /// <param name="template">The template to validate</param>
    /// <exception cref="BusinessException">Thrown when validation fails</exception>
    private static void ValidateProjectTemplate(ProjectTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            throw new BusinessException("Template ID is required");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new BusinessException($"Template name is required for template '{template.Id}'");
        }

        if (string.IsNullOrWhiteSpace(template.Description))
        {
            throw new BusinessException($"Template description is required for template '{template.Id}'");
        }

        if (string.IsNullOrWhiteSpace(template.RepositoryUrl))
        {
            throw new BusinessException($"Repository URL is required for template '{template.Id}'");
        }

        // Validate repository URL format
        if (!Uri.TryCreate(template.RepositoryUrl, UriKind.Absolute, out Uri? uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new BusinessException(
                $"Invalid repository URL format for template '{template.Id}': {template.RepositoryUrl}");
        }
    }
}