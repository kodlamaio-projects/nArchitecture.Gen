using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class QueryTemplateData : ITemplateData
{
    public string QueryName { get; set; }
    public string FeatureName { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
}
