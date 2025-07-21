using Core.CodeGen.TemplateEngine;

namespace NArchitecture.Gen.Domain.Features.CodeGeneration.ValueObjects;

public class QueryTemplateData : ITemplateData
{
    public required string QueryName { get; set; }
    public required string FeatureName { get; set; }
    public required string ProjectName { get; set; } = "NArchitecture.Starter";
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
    public bool IsApiEndpointUsed { get; set; } = true;
}
