using Core.CodeGen.TemplateEngine;

namespace NArchitecture.Gen.Domain.Features.ProjectManagement.ValueObjects;

public class NewProjectData : ITemplateData
{
    public required string ProjectName { get; set; }
}
