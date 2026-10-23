using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps;

/// <summary>Swashbuckle 10 / Microsoft.OpenApi 2 namespace changes and Program.cs sanity checks.</summary>
public sealed class WebApiStep : IUpgradeStep
{
    private static readonly string[] _oldOpenApiNamespaces =
    [
        "Microsoft.OpenApi.Models",
        "Microsoft.OpenApi.Any",
        "Microsoft.OpenApi.Interfaces",
    ];

    public string Id => "web-api";
    public string Title => "Updating Web API for Swashbuckle 10";

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        int replaced = 0;
        foreach (string file in context.ProjectAndTestCSharpFiles.ToList())
        {
            string text = context.Files.Read(file);
            if (!text.Contains("Microsoft.OpenApi."))
                continue;

            CompilationUnitSyntax root = context.Files.GetTree(file).GetCompilationUnitRoot();
            bool changed = false;
            foreach (string oldNamespace in _oldOpenApiNamespaces)
            {
                if (!UsingDirectiveHelper.HasUsing(root, oldNamespace))
                    continue;
                root = UsingDirectiveHelper.RemoveUsing(root, oldNamespace);
                root = UsingDirectiveHelper.AddUsing(root, "Microsoft.OpenApi");
                changed = true;
                replaced++;
            }
            if (changed)
                context.Files.Write(file, root.ToFullString());

            if (
                Path.GetFileName(file).Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
                && text.Contains("BearerSecurityRequirementOperationFilter")
                && !text.Contains("AddSecurityDefinition(")
            )
                context.Report.Manual(
                    ManualWorkKind.WebApi,
                    file,
                    0,
                    "`BearerSecurityRequirementOperationFilter` 2.0 only adds the requirement; declare the scheme with `opt.AddSecurityDefinition(\"Bearer\", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = \"Bearer\" })`."
                );
        }

        if (replaced > 0)
            context.Report.Add(Title, $"`using Microsoft.OpenApi.Models;` → `using Microsoft.OpenApi;` in {replaced} file(s)");
        return Task.CompletedTask;
    }
}
