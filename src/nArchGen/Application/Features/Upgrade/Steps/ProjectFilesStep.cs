using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Application.Features.Upgrade.Steps;

/// <summary>Target framework, package versions, global.json, tool manifest and GitHub workflows.</summary>
public sealed partial class ProjectFilesStep : IUpgradeStep
{
    private const string _title = "Updating project files, SDK and tooling";

    public string Id => "project-files";
    public string Title => _title;

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        bool upgradeTests = !context.Options.KeepTestFramework;
        string? packagesProps = Path.Combine(context.SolutionPath, "Directory.Packages.props");
        bool usesCpm = context.Files.Exists(packagesProps);

        foreach (string csproj in context.Files.FilesWithExtension(".csproj"))
            updateProject(context, csproj, usesCpm, upgradeTests);

        if (usesCpm)
            updatePackageVersions(context, packagesProps, upgradeTests);

        updateGlobalJson(context, upgradeTests);
        updateToolManifest(context);
        updateWorkflows(context);
        return Task.CompletedTask;
    }

    private static void updateProject(UpgradeContext context, string csprojPath, bool usesCpm, bool upgradeTests)
    {
        string text = context.Files.Read(csprojPath);
        XDocument doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        XElement root = doc.Root!;
        MsBuildPropertyIndex properties = MsBuildPropertyIndex.Build(text);
        bool changed = false;
        string relative = Path.GetRelativePath(context.SolutionPath, csprojPath).Replace('\\', '/');
        bool isWeb = root.Attribute("Sdk")?.Value.Contains("Sdk.Web", StringComparison.OrdinalIgnoreCase) == true;

        foreach (XElement tfm in root.Descendants("TargetFramework"))
        {
            if (tfm.Value.Trim() != PackageCatalog.TargetFramework)
            {
                context.Report.Add(_title, $"`{relative}`: `{tfm.Value.Trim()}` → `{PackageCatalog.TargetFramework}`");
                tfm.Value = PackageCatalog.TargetFramework;
                changed = true;
            }
        }

        bool hadMediatR = false,
            hadAutoMapper = false;
        XElement? packageGroup = null;
        foreach (XElement reference in root.Descendants("PackageReference").ToList())
        {
            string id = reference.Attribute("Include")?.Value ?? string.Empty;
            packageGroup ??= reference.Parent;

            if (PackageCatalog.RemovedPackages.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                hadMediatR |= id.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase);
                hadAutoMapper |= id.StartsWith("AutoMapper", StringComparison.OrdinalIgnoreCase);
                removeWithWhitespace(reference);
                context.Report.Add(_title, $"`{relative}`: removed `{id}`");
                changed = true;
                continue;
            }
            if (isWeb && PackageCatalog.RedundantInWebProjects.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                removeWithWhitespace(reference);
                context.Report.Add(_title, $"`{relative}`: removed `{id}` (part of the web SDK)");
                changed = true;
                continue;
            }

            XAttribute? versionAttribute = reference.Attribute("Version");
            string? newVersion = PackageCatalog.ResolveVersion(id, upgradeTests);
            if (versionAttribute is not null && newVersion is not null && properties.Resolve(versionAttribute.Value) != newVersion)
            {
                context.Report.Add(_title, $"`{relative}`: `{id}` {versionAttribute.Value} → {newVersion}");
                versionAttribute.Value = newVersion;
                changed = true;
            }
        }

        if (packageGroup is not null)
        {
            if (hadMediatR && !hasReference(root, PackageCatalog.MediatorPackage))
            {
                addReference(packageGroup, PackageCatalog.MediatorPackage, usesCpm ? null : PackageCatalog.CoreVersion);
                context.Report.Add(_title, $"`{relative}`: added `{PackageCatalog.MediatorPackage}`");
                changed = true;
            }
            if (hadAutoMapper && !hasReference(root, PackageCatalog.MapperlyPackage))
            {
                XElement mapperly = addReference(
                    packageGroup,
                    PackageCatalog.MapperlyPackage,
                    usesCpm ? null : PackageCatalog.MapperlyVersion
                );
                mapperly.SetAttributeValue("PrivateAssets", "all");
                mapperly.SetAttributeValue("ExcludeAssets", "runtime");
                context.Report.Add(_title, $"`{relative}`: added `{PackageCatalog.MapperlyPackage}` (source generator)");
                changed = true;
            }
        }

        if (changed)
            context.Files.Write(csprojPath, serialize(doc, text));
    }

    private static void updatePackageVersions(UpgradeContext context, string propsPath, bool upgradeTests)
    {
        string text = context.Files.Read(propsPath);
        XDocument doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        XElement root = doc.Root!;
        MsBuildPropertyIndex properties = MsBuildPropertyIndex.Build(text);
        bool changed = false;
        XElement? group = null;
        HashSet<string> present = new(StringComparer.OrdinalIgnoreCase);

        foreach (XElement version in root.Descendants("PackageVersion").ToList())
        {
            string id = version.Attribute("Include")?.Value ?? string.Empty;
            group ??= version.Parent;
            present.Add(id);
            if (PackageCatalog.RemovedPackages.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                removeWithWhitespace(version);
                changed = true;
                continue;
            }
            XAttribute? versionAttribute = version.Attribute("Version");
            string? newVersion = PackageCatalog.ResolveVersion(id, upgradeTests);
            // A version written through a property (e.g. Version="$(NArchitectureCoreVersion)") that already
            // resolves to the target is left alone, so a convenience property is not flattened into a literal.
            if (versionAttribute is not null && newVersion is not null && properties.Resolve(versionAttribute.Value) != newVersion)
            {
                versionAttribute.Value = newVersion;
                changed = true;
            }
        }

        if (group is not null)
        {
            foreach (
                (string id, string version) in new[]
                {
                    (PackageCatalog.MediatorPackage, PackageCatalog.CoreVersion),
                    (PackageCatalog.MapperlyPackage, PackageCatalog.MapperlyVersion),
                }
            )
            {
                if (present.Contains(id))
                    continue;
                XElement element = new("PackageVersion", new XAttribute("Include", id), new XAttribute("Version", version));
                insertAfterLast(group, element, "PackageVersion");
                changed = true;
            }
            if (upgradeTests)
                foreach ((string id, string version) in PackageCatalog.TestPackages)
                {
                    XElement? existing = root.Descendants("PackageVersion").FirstOrDefault(v => v.Attribute("Include")?.Value == id);
                    if (existing is null)
                    {
                        insertAfterLast(
                            group,
                            new XElement("PackageVersion", new XAttribute("Include", id), new XAttribute("Version", version)),
                            "PackageVersion"
                        );
                        changed = true;
                    }
                    else if (existing.Attribute("Version")?.Value != version)
                    {
                        existing.SetAttributeValue("Version", version);
                        changed = true;
                    }
                }
        }

        if (changed)
        {
            context.Files.Write(propsPath, serialize(doc, text));
            context.Report.Add(_title, "`Directory.Packages.props`: package versions updated");
        }
    }

    private static void updateGlobalJson(UpgradeContext context, bool upgradeTests)
    {
        string path = Path.Combine(context.SolutionPath, "global.json");
        JsonObject root = context.Files.TryRead(path, out string existing)
            ? JsonNode.Parse(existing)?.AsObject() ?? new JsonObject()
            : new JsonObject();
        bool changed = false;

        JsonObject sdk = root["sdk"] as JsonObject ?? new JsonObject();
        string? version = sdk["version"]?.GetValue<string>();
        if (version is null || !version.StartsWith("10.", StringComparison.Ordinal))
        {
            sdk["version"] = PackageCatalog.SdkVersion;
            sdk["rollForward"] = "latestFeature";
            changed = true;
        }
        root["sdk"] = sdk;

        if (upgradeTests && root["test"] is null)
        {
            root["test"] = new JsonObject { ["runner"] = "Microsoft.Testing.Platform" };
            changed = true;
        }

        if (changed)
        {
            context.Files.Write(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            context.Report.Add(_title, existing.Length == 0 ? "`global.json`: created (SDK 10.0.100)" : "`global.json`: SDK → 10.0.100");
        }
    }

    private static void updateToolManifest(UpgradeContext context)
    {
        string path = Path.Combine(context.SolutionPath, ".config", "dotnet-tools.json");
        if (!context.Files.TryRead(path, out string existing))
            return;
        JsonObject? root = JsonNode.Parse(existing)?.AsObject();
        if (root?["tools"] is not JsonObject tools)
            return;

        bool changed = false;
        foreach ((string id, string version, string command) in PackageCatalog.Tools)
        {
            JsonObject? tool = tools[id] as JsonObject;
            if (tool is null)
            {
                if (id != "narchitecture.gen")
                    continue;
                tool = new JsonObject();
                tools[id] = tool;
            }
            if (tool["version"]?.GetValue<string>() != version)
            {
                tool["version"] = version;
                changed = true;
            }
            string[] commands = tool["commands"]?.AsArray().Select(c => c?.GetValue<string>() ?? string.Empty).ToArray() ?? [];
            if (commands.Length != 1 || commands[0] != command)
            {
                tool["commands"] = new JsonArray(command);
                changed = true;
            }
        }

        if (changed)
        {
            context.Files.Write(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            context.Report.Add(
                _title,
                "`.config/dotnet-tools.json`: dotnet-ef 10.0.13, csharpier 1.3.0 (`csharpier` command), roslynator 1.0.1, narchitecture.gen 2.0.0"
            );
        }
    }

    private static void updateWorkflows(UpgradeContext context)
    {
        string workflowsDir = Path.Combine(context.SolutionPath, ".github", "workflows");
        foreach (string file in context.Files.Files.Where(f => f.StartsWith(workflowsDir, StringComparison.OrdinalIgnoreCase)))
        {
            string text = context.Files.Read(file);
            string updated = DotnetVersionRegex().Replace(text, "global-json-file: global.json");
            updated = updated
                .Replace("actions/setup-dotnet@v3", "actions/setup-dotnet@v4")
                .Replace("actions/checkout@v3", "actions/checkout@v4")
                .Replace("dotnet csharpier --check .", "dotnet csharpier check .");
            if (updated != text)
            {
                context.Files.Write(file, updated);
                context.Report.Add(
                    _title,
                    $"`{Path.GetRelativePath(context.SolutionPath, file).Replace('\\', '/')}`: SDK from global.json, actions v4, csharpier 1.x syntax"
                );
            }
        }
    }

    private static bool hasReference(XElement root, string id) =>
        root.Descendants("PackageReference").Any(r => string.Equals(r.Attribute("Include")?.Value, id, StringComparison.OrdinalIgnoreCase));

    private static XElement addReference(XElement group, string id, string? version)
    {
        XElement element = new("PackageReference", new XAttribute("Include", id));
        if (version is not null)
            element.SetAttributeValue("Version", version);
        insertAfterLast(group, element, "PackageReference");
        return element;
    }

    private static void insertAfterLast(XElement group, XElement element, string name)
    {
        XElement? last = group.Elements(name).LastOrDefault();
        if (last is null)
        {
            group.Add(element);
            return;
        }
        XText? indentation = last.PreviousNode as XText;
        last.AddAfterSelf(element);
        if (indentation is not null)
            element.AddBeforeSelf(new XText(indentation.Value));
    }

    private static void removeWithWhitespace(XElement element)
    {
        if (element.PreviousNode is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
            whitespace.Remove();
        element.Remove();
    }

    private static string serialize(XDocument doc, string originalText)
    {
        string result = doc.Declaration is null
            ? doc.ToString(SaveOptions.DisableFormatting)
            : doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.DisableFormatting);
        if (originalText.StartsWith('﻿'))
            result = '﻿' + result;
        if (originalText.EndsWith('\n') && !result.EndsWith('\n'))
            result += originalText.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return result;
    }

    [GeneratedRegex(@"dotnet-version:\s*['""]?[\d.]+x?['""]?")]
    private static partial Regex DotnetVersionRegex();
}
