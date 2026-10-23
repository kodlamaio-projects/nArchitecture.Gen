using System.Text;
using Core.CodeGen.Code;
using Core.CodeGen.Code.CSharp.Roslyn;

namespace Application.Features.Upgrade.Steps.AutoMapper;

/// <summary>Decides where each profile's mapper lives and how its methods are named, then renders the Mapperly class.</summary>
public sealed class MapperlyMapperGenerator(SourceIndex index)
{
    private static readonly string[] _auditMembers = ["Id", "CreatedDate", "UpdatedDate", "DeletedDate"];

    public void Plan(ProfileInfo profile)
    {
        profile.EntityName = pickEntityName(profile);

        string profileDirectory = Path.GetDirectoryName(profile.FilePath)!;
        if (profile.Namespace.EndsWith(".Profiles", StringComparison.Ordinal))
        {
            profile.MapperNamespace = profile.Namespace[..^".Profiles".Length] + ".Mappers";
            string feature = profile.MapperNamespace.Split('.')[^2];
            profile.MapperClassName = (profile.EntityName ?? feature.ToSingular()) + "Mapper";
            profile.MapperFilePath = Path.Combine(Path.GetDirectoryName(profileDirectory)!, "Mappers", profile.MapperClassName + ".cs");
        }
        else
        {
            profile.MapperNamespace = profile.Namespace;
            string stem = profile.ClassName;
            foreach (string suffix in new[] { "MappingProfiles", "MappingProfile", "Profiles", "Profile" })
                if (stem.EndsWith(suffix, StringComparison.Ordinal) && stem.Length > suffix.Length)
                {
                    stem = stem[..^suffix.Length];
                    break;
                }
            if (stem is "" or "Mapping" or "MappingProfiles" or "MappingProfile" or "Profiles" or "Profile")
                stem = profile.EntityName ?? profile.Namespace.Split('.')[^1];
            profile.MapperClassName = stem + "Mapper";
            profile.MapperFilePath = Path.Combine(profileDirectory, profile.MapperClassName + ".cs");
        }
    }

    /// <summary>Several profiles can land in the same mapper (two profiles of one feature); names must be unique across them.</summary>
    public void PlanMethodNames(IReadOnlyList<ProfileInfo> group)
    {
        HashSet<string> taken = [];
        HashSet<string> rendered = [];
        foreach (ProfileInfo profile in group)
        foreach (MapDefinition map in profile.Maps.Where(m => m.ShouldGenerate))
        {
            if (!rendered.Add($"{map.SourceKey}->{map.DestinationKey}"))
                continue;
            string stem = methodStem(profile, map);
            map.MapMethod = unique(taken, "To" + stem, map.SourceType);
            map.UpdateMethod = "Update" + stem;
            map.ProjectionMethod = "ProjectTo" + stem;
            map.CollectionMethod = "To" + stem + "List";
        }
    }

    public string Render(IReadOnlyList<ProfileInfo> group, UpgradeReport report)
    {
        ProfileInfo profile = group[0];
        StringBuilder sb = new();
        SortedSet<string> usings = new(StringComparer.Ordinal) { "using Riok.Mapperly.Abstractions;" };
        foreach (string directive in group.SelectMany(p => p.UsingDirectives))
            usings.Add(directive);
        foreach (
            string directive in usings
                .OrderBy(u => u.StartsWith("using static", StringComparison.Ordinal))
                .ThenBy(usingSortKey, StringComparer.Ordinal)
        )
            sb.AppendLine(directive);
        sb.AppendLine();
        sb.AppendLine($"namespace {profile.MapperNamespace};");
        sb.AppendLine();
        sb.AppendLine("[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]");
        sb.AppendLine($"public static partial class {profile.MapperClassName}");
        sb.AppendLine("{");

        bool first = true;
        HashSet<string> rendered = [];
        foreach (MapDefinition map in group.SelectMany(p => p.Maps).Where(m => m.ShouldGenerate))
        {
            if (!rendered.Add($"{map.SourceKey}->{map.DestinationKey}"))
                continue;
            if (!first)
                sb.AppendLine();
            first = false;

            List<string> attributes = buildAttributes(map, out List<string> todos);
            foreach (string todo in todos)
            {
                sb.AppendLine($"    // TODO(narch-upgrade): AutoMapper configuration not translated: {todo}");
                report.Manual(
                    ManualWorkKind.AutoMapper,
                    profile.MapperFilePath,
                    0,
                    $"`{map.SourceType}` → `{map.DestinationType}`: translate `{todo}` by hand (see the TODO comment in `{profile.MapperClassName}`)."
                );
            }
            foreach (string attribute in attributes)
                sb.AppendLine($"    {attribute}");
            sb.AppendLine($"    public static partial {map.DestinationType} {map.MapMethod}({map.SourceType} source);");

            if (map.UsedAsUpdate)
            {
                sb.AppendLine();
                foreach (string attribute in attributes)
                    sb.AppendLine($"    {attribute}");
                sb.AppendLine($"    public static partial void {map.UpdateMethod}({map.SourceType} source, {map.DestinationType} target);");
            }
            if (map.UsedAsCollection)
            {
                sb.AppendLine();
                sb.AppendLine(
                    $"    public static partial List<{map.DestinationType}> {map.CollectionMethod}(IEnumerable<{map.SourceType}> source);"
                );
            }
            if (map.UsedAsProjection)
            {
                sb.AppendLine();
                sb.AppendLine(
                    $"    public static partial IQueryable<{map.DestinationType}> {map.ProjectionMethod}(this IQueryable<{map.SourceType}> source);"
                );
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private List<string> buildAttributes(MapDefinition map, out List<string> todos)
    {
        todos = [];
        List<string> attributes = [];
        HashSet<string> ignoredTargets = [];
        HashSet<string> configuredTargets = [];
        bool destinationIsPlain = TypeKey.GenericArguments(map.DestinationType).Length == 0;
        bool sourceIsPlain = TypeKey.GenericArguments(map.SourceType).Length == 0;
        string destinationSimple = TypeKey.Simple(map.DestinationType);
        string sourceSimple = TypeKey.Simple(map.SourceType);

        foreach (MemberMapping member in map.Members)
        {
            switch (member.Kind)
            {
                case MemberMappingKind.IgnoreTarget when member.TargetMember is not null:
                    if (ignoredTargets.Add(member.TargetMember))
                        attributes.Add(
                            $"[MapperIgnoreTarget({memberReference(destinationSimple, member.TargetMember, destinationIsPlain)})]"
                        );
                    configuredTargets.Add(member.TargetMember);
                    break;
                case MemberMappingKind.IgnoreSource when member.SourcePath is not null:
                    attributes.Add($"[MapperIgnoreSource({memberReference(sourceSimple, member.SourcePath, sourceIsPlain)})]");
                    break;
                case MemberMappingKind.MapFromPath when member.TargetMember is not null && member.SourcePath is not null:
                    attributes.Add(
                        $"[MapProperty({memberReference(sourceSimple, member.SourcePath, sourceIsPlain)}, {memberReference(destinationSimple, member.TargetMember, destinationIsPlain)})]"
                    );
                    configuredTargets.Add(member.TargetMember);
                    break;
                default:
                    todos.Add(member.RawText);
                    if (member.TargetMember is not null)
                        configuredTargets.Add(member.TargetMember);
                    break;
            }
        }

        SourceType? destination = destinationIsPlain ? index.Find(destinationSimple) : null;
        SourceType? source = sourceIsPlain ? index.Find(sourceSimple) : null;

        if (destination is not null && index.IsEntity(destination))
        {
            foreach (string audit in _auditMembers)
                if (ignoredTargets.Add(audit))
                    attributes.Add($"[MapperIgnoreTarget(nameof({destinationSimple}.{audit}))]");
            foreach (SourceMember member in index.GetAllMembers(destination))
                if (isNavigation(member) && ignoredTargets.Add(member.Name))
                    attributes.Add($"[MapperIgnoreTarget(nameof({destinationSimple}.{member.Name}))]");
        }

        // Only ignore "missing" target members when every member of the source is visible to the index; a source with
        // a base class from a NuGet package (Core's User<TId>, Entity<TId>) would otherwise hide real matches.
        if (destination is not null && source is not null && knowsAllMembers(source))
        {
            HashSet<string> sourceMembers = index.GetAllMembers(source).Select(m => m.Name).ToHashSet();
            if (index.IsEntity(source))
                sourceMembers.UnionWith(_auditMembers);
            foreach (SourceMember member in index.GetAllMembers(destination))
                if (
                    member.IsSettable
                    && !sourceMembers.Contains(member.Name)
                    && !configuredTargets.Contains(member.Name)
                    && ignoredTargets.Add(member.Name)
                )
                    attributes.Add($"[MapperIgnoreTarget(nameof({destinationSimple}.{member.Name}))]");
        }

        return attributes;
    }

    /// <summary>True when the type and all of its base types are declared in the indexed sources (or are the Core <c>Entity&lt;TId&gt;</c>).</summary>
    private bool knowsAllMembers(SourceType type)
    {
        HashSet<string> visited = [];
        return check(type);

        bool check(SourceType current)
        {
            if (!visited.Add(current.FullName))
                return true;
            foreach (string baseType in current.BaseTypes)
            {
                if (TypeKey.Simple(baseType) == "Entity")
                    continue;
                SourceType? resolved = index.Find(baseType);
                // `User : NArchitecture.Core.Security.Entities.User<Guid>` resolves to itself by simple name; that base is unknown
                if (resolved is not null && resolved.FullName == current.FullName)
                    resolved = null;
                if (resolved is null)
                {
                    // interfaces are harmless, concrete bases from packages are not
                    if (
                        TypeKey.Simple(baseType).StartsWith('I')
                        && TypeKey.Simple(baseType).Length > 1
                        && char.IsUpper(TypeKey.Simple(baseType)[1])
                    )
                        continue;
                    return false;
                }
                if (!check(resolved))
                    return false;
            }
            return true;
        }
    }

    private bool isNavigation(SourceMember member)
    {
        string typeText = member.TypeText;
        if (SourceIndex.TryGetCollectionElementType(typeText, out string element))
            typeText = element;
        SourceType? type = index.Find(typeText);
        return type is not null && index.IsEntity(type);
    }

    private string methodStem(ProfileInfo profile, MapDefinition map)
    {
        string destination = TypeKey.Simple(map.DestinationType);
        SourceType? destinationType = TypeKey.GenericArguments(map.DestinationType).Length == 0 ? index.Find(destination) : null;
        if (profile.EntityName is not null)
        {
            if (destination == profile.EntityName && destinationType is not null && index.IsEntity(destinationType))
                return "Entity";
            string stripped = destination.Replace(profile.EntityName, string.Empty).Replace("GetListListItemDto", "GetListItemDto");
            if (stripped.Length > 0 && char.IsLetter(stripped[0]))
                return stripped;
        }
        return destination;
    }

    private string? pickEntityName(ProfileInfo profile)
    {
        IEnumerable<string> candidates = profile
            .Maps.SelectMany(m => new[] { m.SourceType, m.DestinationType })
            .Where(t => TypeKey.GenericArguments(t).Length == 0)
            .Select(TypeKey.Simple)
            .Where(name => index.Find(name) is SourceType type && index.IsEntity(type));
        return candidates.GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
    }

    private static string unique(HashSet<string> taken, string name, string sourceType)
    {
        // overloads on a different source type are fine; only identical signatures collide
        return taken.Add($"{name}({TypeKey.Normalize(sourceType)})") ? name : name + "From" + TypeKey.Simple(sourceType);
    }

    private static string memberReference(string typeSimpleName, string memberPath, bool typeIsPlain) =>
        typeIsPlain && !memberPath.Contains('.') ? $"nameof({typeSimpleName}.{memberPath})" : $"\"{memberPath}\"";

    private static string usingSortKey(string directive)
    {
        string ns = directive["using ".Length..].TrimEnd(';').Trim();
        bool system = ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);
        return (system ? "0" : "1") + ns;
    }
}
