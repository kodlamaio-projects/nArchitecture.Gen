using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class CommandTemplateData : ITemplateData
{
    public string CommandName { get; set; }
    public string FeatureName { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsTransactionUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
    public string EndPointMethod { get; set; }
}
