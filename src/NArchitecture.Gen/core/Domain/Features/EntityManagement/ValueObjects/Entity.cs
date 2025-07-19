using Core.CodeGen.Code.CSharp.ValueObjects;

namespace NArchitecture.Gen.Domain.Features.EntityManagement.ValueObjects;

public class Entity
{
    public required string Name { get; set; }
    public required string IdType { get; set; }
    public ICollection<PropertyInfo> Properties { get; set; }

    public Entity()
    {
        Properties = Array.Empty<PropertyInfo>();
    }
}
