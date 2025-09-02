using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class DynamicQueryTemplateData : ITemplateData
{
    public required Entity Entity { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
}
