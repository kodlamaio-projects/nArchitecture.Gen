using Core.CodeGen.TemplateEngine;

namespace Domain.ValueObjects;

public class CrudTemplateData : ITemplateData
{
    public required Entity Entity { get; set; }
    public required string DbContextName { get; set; }
    public bool IsCachingUsed { get; set; }
    public bool IsLoggingUsed { get; set; }
    public bool IsTransactionUsed { get; set; }
    public bool IsSecuredOperationUsed { get; set; }
    public bool IsDynamicQueryUsed { get; set; }
    public bool IsMenuRoleIncluded { get; set; }
    public string? CustomOperationClaimPath { get; set; }
    public bool IsCustomOperationClaimPath => !string.IsNullOrEmpty(CustomOperationClaimPath);

    public OperationClaimType OperationClaimType { get; set; } = OperationClaimType.Numeric;
    public string OperationClaimTypeString { get; set; } = "Numeric";
}

public enum OperationClaimType
{
    Numeric,
    Guid
}
