using Core.CodeGen.File;

namespace Core.CodeGen.TemplateEngine;

public class TemplateEngine : ITemplateEngine
{
    private readonly ITemplateRenderer _templateRenderer;

    public TemplateEngine(ITemplateRenderer templateRenderer)
    {
        _templateRenderer = templateRenderer;
    }

    public string TemplateExtension => _templateRenderer.TemplateExtension;

    public async Task<string> RenderAsync(string template, ITemplateData templateData) =>
        await _templateRenderer.RenderAsync(template, templateData);

    public async Task<string> RenderFileAsync(
        string templateFilePath,
        string templateDir,
        Dictionary<string, string> replacePathVariable,
        string outputDir,
        ITemplateData templateData
    )
    {
        string templateFileText = await System.IO.File.ReadAllTextAsync(templateFilePath);

        string newRenderedFileText = await _templateRenderer.RenderAsync(
            templateFileText,
            templateData
        );
        string newRenderedFilePath = await _templateRenderer.RenderAsync(
            template: getOutputFilePath(
                templateFilePath,
                templateDir,
                replacePathVariable,
                outputDir
            ),
            templateData
        );

        await FileHelper.CreateFileAsync(newRenderedFilePath, newRenderedFileText);
        return newRenderedFilePath;
    }

    public async Task<ICollection<string>> RenderFileAsync(
        IList<string> templateFilePaths,
        string templateDir,
        Dictionary<string, string> replacePathVariable,
        string outputDir,
        ITemplateData templateData
    )
    {
        List<string> newRenderedFilePaths = new();
        foreach (string templateFilePath in templateFilePaths)
        {
            string newRenderedFilePath = await RenderFileAsync(
                templateFilePath,
                templateDir,
                replacePathVariable,
                outputDir,
                templateData
            );
            newRenderedFilePaths.Add(newRenderedFilePath);
        }

        return newRenderedFilePaths;
    }

    private string getOutputFilePath(
        string templateFilePath,
        string templateDir,
        Dictionary<string, string> replacePathVariable,
        string outputDir
    )
    {
        string outputFilePath = templateFilePath;
        foreach (KeyValuePair<string, string> replacePathVariableItem in replacePathVariable)
            outputFilePath = outputFilePath.Replace(
                replacePathVariableItem.Key,
                replacePathVariableItem.Value
            );
        outputFilePath = outputFilePath
            .Replace(templateDir, outputDir)
            .Replace(oldValue: $".{_templateRenderer.TemplateExtension}", string.Empty);
        return outputFilePath;
    }
}
