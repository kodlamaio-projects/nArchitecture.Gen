using Scriban.Functions;

namespace Core.CodeGen.TemplateEngine.Scriban;

public class ScribanGuidFunctionsExtensions : BuiltinFunctions
{
    public static string New() => System.Guid.NewGuid().ToString();
}
