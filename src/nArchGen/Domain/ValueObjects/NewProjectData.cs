using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class NewProjectData : ITemplateData
{
    public required string ProjectName { get; set; }
}
