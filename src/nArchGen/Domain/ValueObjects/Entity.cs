using Core.CodeGen.Code.CSharp.ValueObjects;

namespace Domain.ValueObjects;

public class Entity
{
    public string Name { get; set; }
    public string IdType { get; set; }
    public ICollection<PropertyInfo> Properties { get; set; }

    public Entity()
    {
        Properties = Array.Empty<PropertyInfo>();
    }
}
