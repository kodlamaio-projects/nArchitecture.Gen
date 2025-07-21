using Core.CodeGen.TemplateEngine;
using NArchitecture.Gen.Domain.Features.EntityManagement.ValueObjects;

namespace NArchitecture.Gen.Domain.Features.CodeGeneration.ValueObjects;

public class CrudTemplateData : ITemplateData
{
    public required Entity Entity { get; set; }
    public required string DbContextName { get; set; }
    public required string ProjectName { get; set; } = "NArchitecture.Starter";
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsTransactionUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
}
