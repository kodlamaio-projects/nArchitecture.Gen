namespace Core.CodeGen.TemplateEngine;

public interface ITemplateEngine
{
    public string TemplateExtension { get; }
    public Task<string> RenderAsync(string template, ITemplateData templateData);

    public Task<string> RenderFileAsync(
        string templateFilePath,
        string templateDir,
        Dictionary<string, string> replacePathVariable,
        string outputDir,
        ITemplateData templateData
    );

    public Task<ICollection<string>> RenderFileAsync(
        IList<string> templateFilePaths,
        string templateDir,
        Dictionary<string, string> replacePathVariable,
        string outputDir,
        ITemplateData templateData
    );
}
