using Core.CodeGen.TemplateEngine;

namespace NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;

public class TemplateConfiguration : ITemplateData
{
    public required string Version { get; set; }
    public required List<ProjectTemplate> Templates { get; set; } = new();
    public required TemplateConfigurationSettings Settings { get; set; } = new();
}

public class TemplateConfigurationSettings
{
    public bool IsDebugMode { get; set; }
    public string DefaultTemplateId { get; set; } = "minimal";
}