using System.Runtime.CompilerServices;
using Application.Features.Generate.Rules;
using Core.CodeGen.File;
using Core.CodeGen.TemplateEngine;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;

namespace Application.Features.Ai.Commands.Init;

/// <summary>
/// Writes AGENTS.md and the instruction files of the selected AI tools, which point to it, into a solution; optionally also
/// the skills and file-scoped rules of those tools.
/// </summary>
public class InitAiCommand : IStreamRequest<InitializedAiResponse>
{
    public string SolutionPath { get; set; } = null!;
    public ICollection<AiTool> Tools { get; set; } = [];

    /// <summary>File for <see cref="AiTool.Custom"/>, relative to the solution root; its extension is turned into <c>.md</c>.</summary>
    public string? CustomPath { get; set; }

    /// <summary>Also writes the skills and file-scoped rules for the selected tools.</summary>
    public bool IncludeSkills { get; set; }

    public bool Force { get; set; }

    /// <param name="RelativePath">Output path relative to the solution root, with <c>/</c> separators.</param>
    /// <param name="FrontMatter">Written before the rendered template, for rules whose front matter depends on the tool.</param>
    private sealed record PlannedFile(string TemplateFilePath, string RelativePath, string? FrontMatter = null);

    public class InitAiCommandHandler : IStreamRequestHandler<InitAiCommand, InitializedAiResponse>
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly GenerateBusinessRules _businessRules;

        public InitAiCommandHandler(ITemplateEngine templateEngine, GenerateBusinessRules businessRules)
        {
            _templateEngine = templateEngine;
            _businessRules = businessRules;
        }

        public async IAsyncEnumerable<InitializedAiResponse> Handle(
            InitAiCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            InitializedAiResponse response = new();

            List<AiToolFile> toolFiles = [];
            foreach (AiTool tool in request.Tools.Distinct().Order())
            {
                string? path =
                    tool == AiTool.Custom ? normalizeCustomPath(request.CustomPath, response.Warnings) : AiToolFiles.PathOf(tool);
                if (path is not null)
                    toolFiles.Add(new AiToolFile(tool, path));
            }

            AiTemplateData templateData = readSolution(request.SolutionPath);
            templateData.ToolFiles = toolFiles;
            templateData.IsCodexSelected = request.Tools.Contains(AiTool.Codex);

            List<PlannedFile> files =
            [
                new(templateFilePathOf("AGENTS.md.sbn"), AiToolFiles.Agents),
                .. toolFiles.Select(file => new PlannedFile(templateFilePathOf("Tools", templateFileNameOf(file.Tool)), file.Path)),
            ];

            if (request.IncludeSkills)
            {
                templateData.SkillRoots = [.. AiToolFiles.SkillRootsOf(request.Tools)];
                files.AddRange(
                    from root in templateData.SkillRoots
                    from skill in AiInstructions.Skills
                    select new PlannedFile(templateFilePathOf("Skills", $"{skill}.md.sbn"), $"{root}/{skill}/SKILL.md")
                );

                foreach (AiRule rule in AiInstructions.Rules)
                foreach (AiTool tool in request.Tools.Distinct().Order())
                    if (AiToolFiles.RulePathOf(tool, rule) is { } rulePath)
                    {
                        templateData.RuleFiles.Add(new AiToolFile(tool, rulePath));
                        files.Add(
                            new PlannedFile(
                                templateFilePathOf("Rules", $"{rule.Name}.md.sbn"),
                                rulePath,
                                AiToolFiles.RuleFrontMatterOf(tool, rule)
                            )
                        );
                    }
            }

            string[] outputPaths = [.. files.Select(file => outputPathOf(request.SolutionPath, file.RelativePath))];
            await _businessRules.FilesShouldNotExistUnlessForced(outputPaths, request.Force);

            response.CurrentStatusMessage = "Writing AI instruction files...";
            yield return response;

            List<string> newFilePaths = [];
            List<string> updatedFilePaths = [];
            foreach ((PlannedFile file, string outputPath) in files.Zip(outputPaths))
            {
                string template = await File.ReadAllTextAsync(file.TemplateFilePath, cancellationToken);
                string content = await _templateEngine.RenderAsync(template, templateData);
                if (file.FrontMatter is not null)
                    content = $"{file.FrontMatter}\n{content}".ReplaceLineEndings();

                (CodeFile.Exists(outputPath) ? updatedFilePaths : newFilePaths).Add(outputPath);
                await FileHelper.CreateFileAsync(outputPath, content);
            }
            response.LastOperationMessage = "AI instruction files have been written.";

            if (templateData.IsCodexSelected)
                response.OutputMessage = "Codex reads [blue]AGENTS.md[/] directly; it needs no file of its own.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFilePaths;
            response.UpdatedFilePathsResult = updatedFilePaths;
            yield return response;
        }

        private static string? normalizeCustomPath(string? customPath, ICollection<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(customPath))
                return null;

            string path = AiToolFiles.NormalizeCustomPath(customPath, out string? warning);
            if (warning is not null)
                warnings.Add(warning);
            return path;
        }

        private static string templateFileNameOf(AiTool tool) =>
            tool switch
            {
                AiTool.Claude => "Claude.md.sbn",
                AiTool.Copilot => "Copilot.md.sbn",
                AiTool.Cursor => "Cursor.mdc.sbn",
                AiTool.Custom => "Custom.md.sbn",
                _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "The tool has no file of its own."),
            };

        private static string templateFilePathOf(params string[] pathItems) =>
            PlatformHelper.SecuredPathJoin([DirectoryHelper.AssemblyDirectory, Templates.Paths.Ai, .. pathItems]);

        private static string outputPathOf(string solutionPath, string relativePath) =>
            Path.GetFullPath(Path.Combine(solutionPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>Collects the facts AGENTS.md mentions: projects, DbContexts, tests and local tools.</summary>
        private static AiTemplateData readSolution(string solutionPath)
        {
            string? solutionFile = Directory
                .EnumerateFiles(solutionPath)
                .Where(path =>
                    path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                )
                .Order()
                .FirstOrDefault();

            string srcPath = Path.Combine(solutionPath, "src");
            AiProject[] projects = Directory.Exists(srcPath)
                ?
                [
                    .. Directory
                        .GetDirectories(srcPath)
                        .Where(path => Path.GetFileName(path) != "corePackages")
                        .Order()
                        .Select(path => new AiProject { Name = Path.GetFileName(path), DbContexts = readDbContexts(path) }),
                ]
                : [];

            string? toolManifest = new[]
            {
                Path.Combine(solutionPath, ".config", "dotnet-tools.json"),
                Path.Combine(solutionPath, "dotnet-tools.json"),
            }
                .Where(File.Exists)
                .Select(File.ReadAllText)
                .FirstOrDefault();

            return new AiTemplateData
            {
                SolutionName = solutionFile is not null
                    ? Path.GetFileNameWithoutExtension(solutionFile)
                    : new DirectoryInfo(solutionPath).Name,
                Projects = projects,
                HasTests = Directory.Exists(Path.Combine(solutionPath, "tests")),
                HasToolManifest = toolManifest is not null,
                HasCsharpier = toolManifest?.Contains("\"csharpier\"", StringComparison.OrdinalIgnoreCase) == true,
                IsGeneratorLocalTool = toolManifest?.Contains("\"narchitecture.gen\"", StringComparison.OrdinalIgnoreCase) == true,
            };
        }

        private static string[] readDbContexts(string projectPath)
        {
            string contextsPath = Path.Combine(projectPath, "Persistence", "Contexts");
            return Directory.Exists(contextsPath)
                ? [.. Directory.GetFiles(contextsPath, "*.cs").Select(path => Path.GetFileNameWithoutExtension(path)).Order()]
                : [];
        }
    }
}
