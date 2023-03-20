using Scriban;
using Scriban.Runtime;

namespace Core.CodeGen.TemplateEngine.Scriban;

public class ScribanTemplateRenderer : ITemplateRenderer
{
    private readonly ScribanBuiltinFunctionsExtensions _builtinFunctionsExtensions;

    public ScribanTemplateRenderer()
    {
        _builtinFunctionsExtensions = new ScribanBuiltinFunctionsExtensions();
    }

    public string TemplateExtension => "sbn";

    public async Task<string> RenderAsync(string template, ITemplateData data)
    {
        TemplateContext templateContext = new();
        templateContext.PushGlobal(_builtinFunctionsExtensions);
        ScriptObject dataScriptObject = new();
        dataScriptObject.Import(data);
        templateContext.PushGlobal(dataScriptObject);

        string renderResult = await Template.Parse(template).RenderAsync(templateContext);
        return renderResult;
    }
}
