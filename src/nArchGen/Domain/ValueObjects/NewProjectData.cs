using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class NewProjectData : ITemplateData
{
    public string ProjectName { get; set; }
}
