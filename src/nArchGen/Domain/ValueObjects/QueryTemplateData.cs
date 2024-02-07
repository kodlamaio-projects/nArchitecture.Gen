using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class QueryTemplateData : ITemplateData
{
    public required string QueryName { get; set; }
    public required string FeatureName { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
}
