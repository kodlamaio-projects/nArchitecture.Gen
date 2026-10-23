using Application.Features.Upgrade.Steps.AutoMapper;
using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps;

/// <summary>AutoMapper profiles → Mapperly mappers, <c>IMapper</c> call sites → static mapper calls.</summary>
public sealed class AutoMapperStep : IUpgradeStep
{
    public string Id => "automapper";
    public string Title => "Replacing AutoMapper with Mapperly";

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        MapRegistry registry = AutoMapperProfileParser.Parse(context);
        SourceIndex index = context.Files.Index;
        MapCallRewriter rewriter = new(registry, index, context.Report);
        List<string> files = context.ProjectAndTestCSharpFiles.ToList();

        foreach (ProfileInfo profile in registry.Profiles)
        foreach ((int line, string reason) in profile.UnconvertibleCalls)
            context.Report.Manual(ManualWorkKind.AutoMapper, profile.FilePath, line, reason);

        foreach (string file in files)
            rewriter.Scan(file, context.Files.GetTree(file).GetCompilationUnitRoot());

        MapperlyMapperGenerator generator = new(index);
        foreach (ProfileInfo profile in registry.Profiles)
            generator.Plan(profile);

        foreach (
            IGrouping<string, ProfileInfo> grouping in registry.Profiles.GroupBy(p => p.MapperFilePath, StringComparer.OrdinalIgnoreCase)
        )
        {
            List<ProfileInfo> group = grouping.ToList();
            generator.PlanMethodNames(group);
            string source = generator.Render(group, context.Report);
            string mapperFilePath = group[0].MapperFilePath;
            string profileNames = string.Join(", ", group.Select(p => $"`{relative(context, p.FilePath)}`"));
            if (context.Files.Exists(mapperFilePath))
            {
                context.Report.Manual(
                    ManualWorkKind.AutoMapper,
                    mapperFilePath,
                    0,
                    $"A file already exists where the mapper for {profileNames} would be generated; it was left untouched. Merge the generated methods by hand."
                );
                continue;
            }
            context.Files.Write(mapperFilePath, source);
            int methodCount = group.SelectMany(p => p.Maps).Count(m => m.ShouldGenerate);
            context.Report.Add(Title, $"{profileNames} → `{relative(context, mapperFilePath)}` ({methodCount} mapping method(s))");
        }

        HashSet<string> profileFiles = registry.Profiles.Select(p => p.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int rewrittenFiles = 0;
        foreach (string file in files)
        {
            if (profileFiles.Contains(file))
                continue;
            CompilationUnitSyntax root = context.Files.GetTree(file).GetCompilationUnitRoot();
            CompilationUnitSyntax updated = rewriter.Rewrite(file, root, out bool changed);
            if (!changed)
                continue;
            context.Files.Write(file, updated.ToFullString());
            rewrittenFiles++;
        }
        if (rewrittenFiles > 0)
            context.Report.Add(Title, $"`IMapper` usages rewritten and mapper plumbing removed in {rewrittenFiles} file(s)");

        foreach (ProfileInfo profile in registry.Profiles)
        {
            if (profile.UnconvertibleCalls.Count > 0)
            {
                context.Report.Manual(
                    ManualWorkKind.AutoMapper,
                    profile.FilePath,
                    0,
                    "Profile kept because it contains mappings that could not be translated; delete it once they are moved to the mapper."
                );
                continue;
            }
            context.Files.Delete(profile.FilePath);
            context.Report.Add(Title, $"deleted `{relative(context, profile.FilePath)}`");
        }

        if (registry.Profiles.Count == 0)
            context.Report.Add(Title, "no AutoMapper profiles found");
        return Task.CompletedTask;
    }

    private static string relative(UpgradeContext context, string path) =>
        Path.GetRelativePath(context.SolutionPath, path).Replace('\\', '/');
}
