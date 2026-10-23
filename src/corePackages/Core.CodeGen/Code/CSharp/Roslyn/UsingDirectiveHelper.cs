using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.CodeGen.Code.CSharp.Roslyn;

public static class UsingDirectiveHelper
{
    public static bool HasUsing(CompilationUnitSyntax root, string ns) =>
        root.Usings.Any(u => u.Alias is null && !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && u.Name?.ToString() == ns);

    /// <summary>Adds <c>using ns;</c> keeping the existing alphabetical order (System namespaces first, static usings last).</summary>
    public static CompilationUnitSyntax AddUsing(CompilationUnitSyntax root, string ns)
    {
        if (HasUsing(root, ns))
            return root;

        UsingDirectiveSyntax directive = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(ns).WithLeadingTrivia(SyntaxFactory.Space))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        int insertAt = root.Usings.Count;
        for (int i = 0; i < root.Usings.Count; i++)
        {
            UsingDirectiveSyntax existing = root.Usings[i];
            if (existing.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) || existing.Alias is not null)
            {
                insertAt = i;
                break;
            }
            if (compare(ns, existing.Name?.ToString() ?? string.Empty) < 0)
            {
                insertAt = i;
                break;
            }
        }

        SyntaxList<UsingDirectiveSyntax> usings = root.Usings.Insert(insertAt, directive);
        if (insertAt == 0 && root.Usings.Count > 0)
        {
            // keep file-level leading trivia (BOM/comments) on the first directive
            SyntaxTriviaList leading = root.Usings[0].GetLeadingTrivia();
            usings = usings.Replace(usings[0], usings[0].WithLeadingTrivia(leading));
            usings = usings.Replace(usings[1], usings[1].WithLeadingTrivia(SyntaxTriviaList.Empty));
        }
        return root.WithUsings(usings);
    }

    public static CompilationUnitSyntax RemoveUsing(CompilationUnitSyntax root, string ns)
    {
        UsingDirectiveSyntax? directive = root.Usings.FirstOrDefault(u =>
            u.Alias is null && !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && u.Name?.ToString() == ns
        );
        if (directive is null)
            return root;

        SyntaxTriviaList leading = directive.GetLeadingTrivia();
        CompilationUnitSyntax updated = root.RemoveNode(directive, SyntaxRemoveOptions.KeepNoTrivia)!;
        if (ReferenceEquals(directive, root.Usings[0]) && updated.Usings.Count > 0)
            updated = updated.WithUsings(updated.Usings.Replace(updated.Usings[0], updated.Usings[0].WithLeadingTrivia(leading)));
        return updated;
    }

    public static CompilationUnitSyntax ReplaceUsing(CompilationUnitSyntax root, string oldNs, string newNs)
    {
        if (!HasUsing(root, oldNs))
            return root;
        root = RemoveUsing(root, oldNs);
        return AddUsing(root, newNs);
    }

    private static int compare(string a, string b)
    {
        bool aSystem = a == "System" || a.StartsWith("System.", StringComparison.Ordinal);
        bool bSystem = b == "System" || b.StartsWith("System.", StringComparison.Ordinal);
        if (aSystem != bSystem)
            return aSystem ? -1 : 1;
        return string.Compare(a, b, StringComparison.Ordinal);
    }
}
