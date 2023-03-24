namespace Core.CodeGen.Code.CSharp.ValueObjects;

public class PropertyInfo
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public string AccessModifier { get; set; }
    public string? NameSpace { get; set; }

    public PropertyInfo()
    {
    }

    public PropertyInfo(string name, string typeName, string accessModifier, string? nameSpace = null)
    {
        Name = name;
        TypeName = typeName;
        AccessModifier = accessModifier;
        NameSpace = nameSpace;
    }
}
