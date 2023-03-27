namespace Core.CodeGen.Code.CSharp.ValueObjects;

public class PropertyInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string AccessModifier { get; set; }
    public string? NameSpace { get; set; }

    public PropertyInfo() { }

    public PropertyInfo(string name, string type, string accessModifier, string? nameSpace = null)
    {
        Name = name;
        Type = type;
        AccessModifier = accessModifier;
        NameSpace = nameSpace;
    }
}
