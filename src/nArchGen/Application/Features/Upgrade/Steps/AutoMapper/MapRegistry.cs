using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps.AutoMapper;

public enum MemberMappingKind
{
    /// <summary><c>ForMember(d => d.X, o => o.MapFrom(s => s.A.B))</c></summary>
    MapFromPath,

    /// <summary><c>ForMember(d => d.X, o => o.Ignore())</c></summary>
    IgnoreTarget,

    /// <summary><c>ForSourceMember(s => s.X, o => o.DoNotValidate())</c></summary>
    IgnoreSource,

    /// <summary>Anything Mapperly cannot express with an attribute; carried over as a TODO comment.</summary>
    Unsupported,
}

public sealed record MemberMapping(MemberMappingKind Kind, string? TargetMember, string? SourcePath, string RawText);

/// <summary>An AutoMapper <c>Profile</c> class and the Mapperly mapper that replaces it.</summary>
public sealed class ProfileInfo
{
    public required string ClassName { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required ClassDeclarationSyntax Declaration { get; init; }
    public required IReadOnlyList<string> UsingDirectives { get; init; }
    public List<MapDefinition> Maps { get; } = [];
    public List<(int Line, string Reason)> UnconvertibleCalls { get; } = [];

    public string MapperClassName { get; set; } = string.Empty;
    public string MapperNamespace { get; set; } = string.Empty;
    public string MapperFilePath { get; set; } = string.Empty;
    public string? EntityName { get; set; }
}

/// <summary>One mapping direction (<c>CreateMap&lt;S, D&gt;</c> or its <c>ReverseMap()</c>).</summary>
public sealed class MapDefinition
{
    public required ProfileInfo Profile { get; init; }
    public required string SourceType { get; init; }
    public required string DestinationType { get; init; }
    public required bool IsReverse { get; init; }
    public required int Line { get; init; }
    public MapDefinition? Counterpart { get; set; }

    public List<MemberMapping> Members { get; } = [];

    public bool UsedAsMap { get; set; }
    public bool UsedAsUpdate { get; set; }
    public bool UsedAsProjection { get; set; }
    public bool UsedAsCollection { get; set; }
    public bool IsUsed => UsedAsMap || UsedAsUpdate || UsedAsProjection || UsedAsCollection;

    /// <summary>Forward maps are always kept unless only their reverse direction is used; reverse maps only when used.</summary>
    public bool ShouldGenerate => IsUsed || (!IsReverse && Counterpart?.IsUsed != true);

    public string MapMethod { get; set; } = string.Empty;
    public string UpdateMethod { get; set; } = string.Empty;
    public string ProjectionMethod { get; set; } = string.Empty;
    public string CollectionMethod { get; set; } = string.Empty;

    public string SourceKey => TypeKey.Normalize(SourceType);
    public string DestinationKey => TypeKey.Normalize(DestinationType);
}

public static class TypeKey
{
    public static string Normalize(string typeText)
    {
        string text = new(typeText.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return text.TrimEnd('?');
    }

    public static string Simple(string typeText) => SourceIndex.StripGenericArguments(typeText);

    public static string GenericArguments(string typeText)
    {
        string text = Normalize(typeText);
        int start = text.IndexOf('<');
        return start < 0 ? string.Empty : text[(start + 1)..^1];
    }

    /// <summary>
    /// Loose type equality for syntax-only analysis: identical text, or same simple name with the same generic
    /// arguments (so <c>Ns.Foo&lt;Guid&gt;</c> matches <c>Foo&lt;Guid&gt;</c> but not <c>Foo</c>).
    /// </summary>
    public static bool Matches(string a, string b)
    {
        string na = Normalize(a),
            nb = Normalize(b);
        if (na == nb)
            return true;
        if (Simple(na) != Simple(nb))
            return false;
        string ga = GenericArguments(na),
            gb = GenericArguments(nb);
        if (ga.Length == 0 && gb.Length == 0)
            return true;
        if (ga.Length == 0 || gb.Length == 0)
            return false;
        string[] pa = ga.Split(','),
            pb = gb.Split(',');
        return pa.Length == pb.Length && pa.Zip(pb).All(pair => Matches(pair.First, pair.Second));
    }
}

public sealed class MapRegistry
{
    public List<ProfileInfo> Profiles { get; } = [];

    public IEnumerable<MapDefinition> All => Profiles.SelectMany(p => p.Maps);

    public IReadOnlyList<MapDefinition> FindByDestination(string destinationType) =>
        All.Where(m => TypeKey.Matches(m.DestinationType, destinationType)).ToList();

    public MapDefinition? Find(string sourceType, string destinationType) =>
        All.FirstOrDefault(m => TypeKey.Matches(m.SourceType, sourceType) && TypeKey.Matches(m.DestinationType, destinationType));

    /// <summary>Picks the definition for a call: exact source match first, then the only candidate for the destination.</summary>
    public MapDefinition? Resolve(string destinationType, string? sourceType)
    {
        IReadOnlyList<MapDefinition> candidates = FindByDestination(destinationType);
        if (candidates.Count == 0)
            return null;
        if (sourceType is not null)
        {
            MapDefinition? exact = candidates.FirstOrDefault(c => TypeKey.Matches(c.SourceType, sourceType));
            if (exact is not null)
                return exact;
        }
        return candidates.Count == 1 ? candidates[0] : null;
    }
}
