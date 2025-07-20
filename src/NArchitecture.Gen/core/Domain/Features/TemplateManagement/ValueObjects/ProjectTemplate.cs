using Core.CodeGen.TemplateEngine;

namespace NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;

public class ProjectTemplate : ITemplateData
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string RepositoryUrl { get; set; }
    public required TemplateInstallationMode InstallationMode { get; set; }
    public string? ReleaseVersion { get; set; }
    public string? BranchName { get; set; }
}

public enum TemplateInstallationMode
{
    Release,
    Branch
}