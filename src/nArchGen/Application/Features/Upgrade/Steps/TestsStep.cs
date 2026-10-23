using System.Xml.Linq;
using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps;

/// <summary>Core.Test 2.0 (`BaseMockRepository` without the mapping profile) and, by default, xunit.v3 + Microsoft Testing Platform.</summary>
public sealed class TestsStep : IUpgradeStep
{
    public string Id => "tests";
    public string Title => "Updating test projects";

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        string testsRoot = Path.Combine(context.SolutionPath, "tests");
        List<string> testProjects = context
            .Files.FilesWithExtension(".csproj")
            .Where(f => f.StartsWith(testsRoot, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!context.Options.KeepTestFramework)
            foreach (string csproj in testProjects)
                upgradeTestFramework(context, csproj);

        int fixedRepositories = 0;
        foreach (string file in context.TestCSharpFiles.ToList())
        {
            string text = context.Files.Read(file);
            if (!text.Contains("BaseMockRepository") && !text.Contains(".Profiles;"))
                continue;

            CompilationUnitSyntax root = context.Files.GetTree(file).GetCompilationUnitRoot();
            CompilationUnitSyntax updated = root;

            foreach (
                GenericNameSyntax generic in root.DescendantNodes()
                    .OfType<GenericNameSyntax>()
                    .Where(g => g.Identifier.Text == "BaseMockRepository" && g.TypeArgumentList.Arguments.Count == 6)
                    .ToList()
            )
            {
                SeparatedSyntaxList<TypeSyntax> arguments = generic.TypeArgumentList.Arguments.RemoveAt(3);
                updated = updated.ReplaceNode(
                    updated.GetCurrentNode(generic) ?? generic,
                    generic.WithTypeArgumentList(SyntaxFactory.TypeArgumentList(arguments))
                );
                fixedRepositories++;
            }

            foreach (
                UsingDirectiveSyntax profileUsing in updated
                    .Usings.Where(u => u.Name?.ToString().EndsWith(".Profiles", StringComparison.Ordinal) == true)
                    .ToList()
            )
            {
                string ns = profileUsing.Name!.ToString();
                bool stillDeclared = context.Files.Index.Types.Any(t => t.Namespace == ns);
                if (!stillDeclared)
                    updated = UsingDirectiveHelper.RemoveUsing(updated, ns);
            }

            if (!ReferenceEquals(updated, root))
                context.Files.Write(file, updated.ToFullString());
        }

        if (fixedRepositories > 0)
            context.Report.Add(Title, $"`BaseMockRepository<…>`: mapping profile type argument removed in {fixedRepositories} place(s)");

        foreach (string file in context.TestCSharpFiles)
        {
            string text = context.Files.Read(file);
            int index = text.IndexOf("new MapperConfiguration(", StringComparison.Ordinal);
            if (index >= 0)
                context.Report.Manual(
                    ManualWorkKind.Tests,
                    file,
                    lineAt(text, index),
                    "AutoMapper `MapperConfiguration` has no Mapperly equivalent; delete it, the handler/service no longer takes a mapper."
                );
        }

        return Task.CompletedTask;
    }

    private void upgradeTestFramework(UpgradeContext context, string csprojPath)
    {
        string text = context.Files.Read(csprojPath);
        XDocument doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        XElement root = doc.Root!;
        bool usesCpm = context.Files.Exists(Path.Combine(context.SolutionPath, "Directory.Packages.props"));
        bool changed = false;
        string relative = Path.GetRelativePath(context.SolutionPath, csprojPath).Replace('\\', '/');

        XElement? packageGroup = null;
        foreach (XElement reference in root.Descendants("PackageReference").ToList())
        {
            string id = reference.Attribute("Include")?.Value ?? string.Empty;
            packageGroup ??= reference.Parent;
            if (PackageCatalog.RemovedTestPackages.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                if (reference.PreviousNode is XText ws && string.IsNullOrWhiteSpace(ws.Value))
                    ws.Remove();
                reference.Remove();
                changed = true;
            }
        }
        packageGroup ??= root.Elements("ItemGroup").FirstOrDefault();
        if (packageGroup is null)
        {
            packageGroup = new XElement("ItemGroup");
            root.Add(packageGroup);
        }

        foreach ((string id, string version) in PackageCatalog.TestPackages)
        {
            XElement? existing = packageGroup
                .Elements("PackageReference")
                .FirstOrDefault(r => string.Equals(r.Attribute("Include")?.Value, id, StringComparison.OrdinalIgnoreCase));
            // Already present in the shape this step would produce (no Version under CPM, or the target Version
            // otherwise): leave it untouched, so a solution already on xunit.v3 is a true no-op.
            bool alreadyCorrect =
                existing is not null && (usesCpm ? existing.Attribute("Version") is null : existing.Attribute("Version")?.Value == version);
            if (alreadyCorrect)
                continue;

            if (existing is not null)
            {
                if (existing.PreviousNode is XText ws && string.IsNullOrWhiteSpace(ws.Value))
                    ws.Remove();
                existing.Remove();
            }

            XElement element = new("PackageReference", new XAttribute("Include", id));
            if (!usesCpm)
                element.SetAttributeValue("Version", version);
            XElement? last = packageGroup.Elements("PackageReference").LastOrDefault();
            if (last is null)
                packageGroup.Add(element);
            else
            {
                XText? indentation = last.PreviousNode as XText;
                last.AddAfterSelf(element);
                if (indentation is not null)
                    element.AddBeforeSelf(new XText(indentation.Value));
            }
            changed = true;
        }

        XElement propertyGroup = root.Elements("PropertyGroup").FirstOrDefault() ?? addPropertyGroup(root);
        foreach (
            (string name, string value) in new[]
            {
                ("OutputType", "Exe"),
                ("IsTestProject", "true"),
                ("UseMicrosoftTestingPlatformRunner", "true"),
                ("TestingPlatformDotnetTestSupport", "true"),
            }
        )
        {
            XElement? existing = propertyGroup.Element(name);
            if (existing is null)
            {
                XElement element = new(name, value);
                XElement? last = propertyGroup.Elements().LastOrDefault();
                if (last is null)
                    propertyGroup.Add(element);
                else
                {
                    XText? indentation = last.PreviousNode as XText;
                    last.AddAfterSelf(element);
                    if (indentation is not null)
                        element.AddBeforeSelf(new XText(indentation.Value));
                }
                changed = true;
            }
            else if (existing.Value != value)
            {
                existing.Value = value;
                changed = true;
            }
        }

        if (changed)
        {
            string result = doc.Declaration is null
                ? doc.ToString(SaveOptions.DisableFormatting)
                : doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.DisableFormatting);
            if (text.StartsWith('﻿'))
                result = '﻿' + result;
            if (text.EndsWith('\n'))
                result += text.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            context.Files.Write(csprojPath, result);
            context.Report.Add(
                Title,
                $"`{relative}`: xunit.v3 + Microsoft Testing Platform (xunit 2, Microsoft.NET.Test.Sdk and coverlet removed)"
            );
        }
    }

    private static XElement addPropertyGroup(XElement root)
    {
        XElement group = new("PropertyGroup");
        root.AddFirst(group);
        return group;
    }

    private static int lineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
}
