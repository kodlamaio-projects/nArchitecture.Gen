using Scriban.Functions;
using Scriban.Runtime;

namespace Core.CodeGen.TemplateEngine.Scriban;

public class ScribanBuiltinFunctionsExtensions : BuiltinFunctions
{
    public ScribanBuiltinFunctionsExtensions()
    {
        addStringFunctionsExtensions();
    }

    private void addStringFunctionsExtensions()
    {
        ScriptObject stringFunctions = new();
        stringFunctions.Import(
            obj: typeof(ScribanStringFunctionsExtensions),
            renamer: member => member.Name.ToLower()
        );
        SetValue(member: "string", stringFunctions, readOnly: true);
    }
}
