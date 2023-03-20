namespace Core.CodeGen.Code.CSharp.ValueObjects;

public class PropertyInfo
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public string AccessModifier { get; set; }

    public PropertyInfo() { }

    public PropertyInfo(string name, string typeName, string accessModifier)
    {
        Name = name;
        TypeName = typeName;
        AccessModifier = accessModifier;
    }
}
