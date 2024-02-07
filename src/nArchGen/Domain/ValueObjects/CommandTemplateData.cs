using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class CommandTemplateData : ITemplateData
{
    public required string CommandName { get; set; }
    public required string FeatureName { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsTransactionUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
    public required string EndPointMethod { get; set; }
}
