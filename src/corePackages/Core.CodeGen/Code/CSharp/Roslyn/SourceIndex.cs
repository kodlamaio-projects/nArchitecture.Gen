using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.CodeGen.Code.CSharp.Roslyn;

/// <summary>A property or field declared on a type, as written in source.</summary>
public sealed record SourceMember(string Name, string TypeText, bool IsSettable);

/// <summary>A class/record/interface found while indexing the sources; syntax only, no semantic model.</summary>
public sealed record SourceType(
    string Name,
    string Namespace,
    string FilePath,
    TypeDeclarationSyntax Declaration,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<SourceMember> Members
)
{
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

    public bool HasMember(string name) => Members.Any(m => m.Name == name);
}

/// <summary>
/// Syntax-level index of every type declared in a set of C# files. It answers the questions a code rewriter needs
/// without compiling the project: which type lives where, what members it has, what it derives from.
/// </summary>
public sealed class SourceIndex
{
    private readonly Dictionary<string, List<SourceType>> _typesByName = new(StringComparer.Ordinal);

    public IReadOnlyList<SourceType> Types { get; }

    private SourceIndex(List<SourceType> types)
    {
        Types = types;
        foreach (SourceType type in types)
        {
            if (!_typesByName.TryGetValue(type.Name, out List<SourceType>? list))
                _typesByName[type.Name] = list = [];
            list.Add(type);
        }
    }

    public static SourceIndex Build(IEnumerable<KeyValuePair<string, SyntaxTree>> trees)
    {
        List<SourceType> types = [];
        foreach ((string path, SyntaxTree tree) in trees)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            foreach (TypeDeclarationSyntax declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                types.Add(describe(declaration, path));
        }

        return new SourceIndex(types);
    }

    /// <summary>Returns the type with this simple name, or null when it is unknown or declared more than once.</summary>
    public SourceType? Find(string simpleName)
    {
        string name = StripGenericArguments(simpleName);
        return _typesByName.TryGetValue(name, out List<SourceType>? list) && list.Count == 1 ? list[0] : null;
    }

    public IReadOnlyList<SourceType> FindAll(string simpleName) =>
        _typesByName.TryGetValue(StripGenericArguments(simpleName), out List<SourceType>? list) ? list : [];

    /// <summary>Members declared on the type and on every base type the index knows about.</summary>
    public IReadOnlyList<SourceMember> GetAllMembers(SourceType type)
    {
        List<SourceMember> members = [];
        HashSet<string> visited = [];
        collect(type);
        return members;

        void collect(SourceType current)
        {
            if (!visited.Add(current.FullName))
                return;
            members.AddRange(current.Members.Where(m => members.All(existing => existing.Name != m.Name)));
            foreach (string baseType in current.BaseTypes)
            {
                SourceType? resolved = Find(baseType);
                if (resolved is not null)
                    collect(resolved);
            }
        }
    }

    /// <summary>True when the type derives (directly or through indexed bases) from <c>Entity&lt;T&gt;</c> or lives in a <c>Domain.Entities</c> namespace.</summary>
    public bool IsEntity(SourceType type)
    {
        HashSet<string> visited = [];
        return check(type);

        bool check(SourceType current)
        {
            if (!visited.Add(current.FullName))
                return false;
            if (current.Namespace.EndsWith("Domain.Entities", StringComparison.Ordinal))
                return true;
            foreach (string baseType in current.BaseTypes)
            {
                if (StripGenericArguments(baseType) == "Entity" || baseType.EndsWith(".Entity", StringComparison.Ordinal))
                    return true;
                SourceType? resolved = Find(baseType);
                if (resolved is not null && check(resolved))
                    return true;
            }
            return false;
        }
    }

    public static string StripGenericArguments(string typeText)
    {
        string text = typeText.Trim().TrimEnd('?');
        int genericStart = text.IndexOf('<');
        if (genericStart >= 0)
            text = text[..genericStart];
        int lastDot = text.LastIndexOf('.');
        return lastDot >= 0 ? text[(lastDot + 1)..] : text;
    }

    /// <summary>Extracts <c>T</c> from <c>List&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>, <c>T[]</c> and friends.</summary>
    public static bool TryGetCollectionElementType(string typeText, out string elementType)
    {
        string text = typeText.Trim().TrimEnd('?');
        if (text.EndsWith("[]", StringComparison.Ordinal))
        {
            elementType = text[..^2];
            return true;
        }

        string[] collectionNames =
        [
            "List",
            "IList",
            "ICollection",
            "IEnumerable",
            "IReadOnlyList",
            "IReadOnlyCollection",
            "Collection",
            "HashSet",
            "ISet",
            "IQueryable",
        ];
        int genericStart = text.IndexOf('<');
        if (genericStart > 0 && text.EndsWith('>'))
        {
            string outer = StripGenericArguments(text);
            if (collectionNames.Contains(outer))
            {
                elementType = text[(genericStart + 1)..^1].Trim();
                return true;
            }
        }

        elementType = string.Empty;
        return false;
    }

    private static SourceType describe(TypeDeclarationSyntax declaration, string path)
    {
        string ns =
            declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(n => n.Name.ToString()).FirstOrDefault()
            ?? string.Empty;
        List<string> baseTypes = declaration.BaseList?.Types.Select(t => t.Type.ToString()).ToList() ?? [];

        List<SourceMember> members = [];
        foreach (PropertyDeclarationSyntax property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            bool settable =
                property.AccessorList?.Accessors.Any(a =>
                    a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)
                )
                ?? false;
            members.Add(new SourceMember(property.Identifier.Text, property.Type.ToString(), settable));
        }
        foreach (FieldDeclarationSyntax field in declaration.Members.OfType<FieldDeclarationSyntax>())
        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            members.Add(
                new SourceMember(
                    variable.Identifier.Text,
                    field.Declaration.Type.ToString(),
                    IsSettable: !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                )
            );
        if (declaration is RecordDeclarationSyntax { ParameterList: not null } record)
            foreach (ParameterSyntax parameter in record.ParameterList.Parameters)
                members.Add(new SourceMember(parameter.Identifier.Text, parameter.Type?.ToString() ?? "object", IsSettable: true));

        return new SourceType(declaration.Identifier.Text, ns, path, declaration, baseTypes, members);
    }
}
