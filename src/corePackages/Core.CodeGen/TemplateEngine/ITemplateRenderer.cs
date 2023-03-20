namespace Core.CodeGen.TemplateEngine;

public interface ITemplateRenderer
{
    public string TemplateExtension { get; }
    public Task<string> RenderAsync(string template, ITemplateData data);
}
