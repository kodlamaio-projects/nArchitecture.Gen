using System.Text.RegularExpressions;
using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps.AutoMapper;

/// <summary>
/// Finds <c>IMapper</c> usages in a file, resolves each to a <see cref="MapDefinition"/> and, in the rewrite pass, replaces
/// them with static Mapperly calls and removes the now unused <c>IMapper</c> plumbing.
/// </summary>
public sealed partial class MapCallRewriter(MapRegistry registry, SourceIndex index, UpgradeReport report)
{
    private readonly TypeInference _inference = new(index);

    private enum CallKind
    {
        Map,
        Update,
        Collection,
        Projection,

        /// <summary><c>_mapper.Map&lt;T&gt;(null)</c>, the placeholder Gen 1.x put in command/query templates.</summary>
        Placeholder,
    }

    private sealed record ResolvedCall(
        InvocationExpressionSyntax Invocation,
        CallKind Kind,
        MapDefinition? Definition,
        ExpressionSyntax Source,
        ExpressionSyntax? Destination,
        string? PlaceholderType = null
    );

    private sealed record UnresolvedCall(InvocationExpressionSyntax Invocation, string Reason);

    /// <summary>First pass: marks which mapping directions and shapes are needed.</summary>
    public void Scan(string filePath, CompilationUnitSyntax root)
    {
        foreach ((ResolvedCall? resolved, _) in analyze(root))
        {
            if (resolved?.Definition is null)
                continue;
            switch (resolved.Kind)
            {
                case CallKind.Map:
                    resolved.Definition.UsedAsMap = true;
                    break;
                case CallKind.Update:
                    resolved.Definition.UsedAsUpdate = true;
                    break;
                case CallKind.Collection:
                    resolved.Definition.UsedAsCollection = true;
                    break;
                case CallKind.Projection:
                    resolved.Definition.UsedAsProjection = true;
                    break;
            }
        }
    }

    /// <summary>Second pass: rewrites calls, removes <c>IMapper</c> members/parameters/arguments and fixes usings.</summary>
    public CompilationUnitSyntax Rewrite(string filePath, CompilationUnitSyntax root, out bool changed)
    {
        changed = false;
        List<(ResolvedCall? Resolved, UnresolvedCall? Unresolved)> calls = analyze(root);
        HashSet<string> mapperNamespaces = [];

        foreach ((_, UnresolvedCall? unresolved) in calls)
            if (unresolved is not null)
                report.Manual(ManualWorkKind.AutoMapper, filePath, lineOf(unresolved.Invocation), unresolved.Reason);

        List<ResolvedCall> resolvedCalls = calls.Where(c => c.Resolved is not null).Select(c => c.Resolved!).ToList();
        if (resolvedCalls.Count > 0)
        {
            List<SyntaxNode> tracked = resolvedCalls
                .Select(c => (SyntaxNode?)statementOf(c.Invocation, c.Kind) ?? c.Invocation)
                .Distinct()
                .ToList();
            root = root.TrackNodes(tracked);

            foreach (ResolvedCall call in resolvedCalls)
            {
                ExpressionSyntax replacement;
                if (call.Kind == CallKind.Placeholder)
                    replacement = SyntaxFactory.ParseExpression($"new {call.PlaceholderType}()");
                else
                {
                    ProfileInfo profile = call.Definition!.Profile;
                    mapperNamespaces.Add(profile.MapperNamespace);
                    string method = call.Kind switch
                    {
                        CallKind.Update => call.Definition.UpdateMethod,
                        CallKind.Collection => call.Definition.CollectionMethod,
                        CallKind.Projection => call.Definition.ProjectionMethod,
                        _ => call.Definition.MapMethod,
                    };
                    string arguments = call.Kind == CallKind.Update ? $"{call.Source}, {call.Destination}" : call.Source.ToString();
                    replacement = SyntaxFactory.ParseExpression($"{profile.MapperClassName}.{method}({arguments})");
                }

                StatementSyntax? statement = statementOf(call.Invocation, call.Kind);
                if (statement is not null)
                {
                    StatementSyntax? current = root.GetCurrentNode(statement);
                    if (current is null)
                        continue;
                    List<StatementSyntax> replacements = rewriteUpdateStatement(current, replacement, call.Destination!);
                    root = root.ReplaceNode(current, replacements);
                }
                else
                {
                    InvocationExpressionSyntax? current = root.GetCurrentNode(call.Invocation);
                    if (current is null)
                        continue;
                    root = root.ReplaceNode(current, replacement.WithTriviaFrom(current));
                }
                changed = true;
            }
        }

        root = removeAutoMapperRegistration(root, ref changed);
        root = removeMapperPlumbing(filePath, root, ref changed);

        if (changed)
        {
            string text = root.ToFullString();
            List<string> autoMapperUsings = root
                .Usings.Select(u => u.Name?.ToString() ?? string.Empty)
                .Where(n => n == "AutoMapper" || n.StartsWith("AutoMapper.", StringComparison.Ordinal))
                .ToList();
            if (autoMapperUsings.Count > 0 && !stillUsesAutoMapper(root))
                foreach (string ns in autoMapperUsings)
                    root = UsingDirectiveHelper.RemoveUsing(root, ns);
            else if (autoMapperUsings.Count > 0)
                report.Manual(
                    ManualWorkKind.AutoMapper,
                    filePath,
                    0,
                    "File still references AutoMapper types after the upgrade; finish the conversion and remove `using AutoMapper;`."
                );

            IEnumerable<string> fileNamespaces = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(n => n.Name.ToString());
            foreach (string ns in mapperNamespaces)
                if (!fileNamespaces.Any(f => f == ns || f.StartsWith(ns + ".", StringComparison.Ordinal)))
                    root = UsingDirectiveHelper.AddUsing(root, ns);
        }

        return root;
    }

    private List<(ResolvedCall? Resolved, UnresolvedCall? Unresolved)> analyze(CompilationUnitSyntax root)
    {
        List<(ResolvedCall?, UnresolvedCall?)> result = [];
        foreach (TypeDeclarationSyntax type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            HashSet<string> mapperNames = mapperIdentifiers(type);
            if (mapperNames.Count == 0)
                continue;
            foreach (InvocationExpressionSyntax invocation in ownDescendants(type).OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                    continue;
                string methodName = memberAccess.Name.Identifier.Text;
                TypeArgumentListSyntax? typeArguments = (memberAccess.Name as GenericNameSyntax)?.TypeArgumentList;

                if (methodName == "Map" && isMapperReceiver(memberAccess.Expression, mapperNames))
                    result.Add(analyzeMap(invocation, typeArguments));
                else if (
                    methodName == "ProjectTo"
                    && typeArguments?.Arguments.Count == 1
                    && invocation.ArgumentList.Arguments.Any(a => mentionsMapper(a.Expression, mapperNames))
                )
                    result.Add(analyzeProjection(invocation, memberAccess.Expression, typeArguments.Arguments[0].ToString()));
            }
        }
        return result;
    }

    private (ResolvedCall?, UnresolvedCall?) analyzeMap(InvocationExpressionSyntax invocation, TypeArgumentListSyntax? typeArguments)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        string call = singleLine(invocation);

        if (typeArguments is null)
        {
            if (arguments.Count < 2)
                return (
                    null,
                    new UnresolvedCall(
                        invocation,
                        $"`{call}`: non-generic `Map(object)` cannot be resolved; call the mapper method directly."
                    )
                );
            ExpressionSyntax source = argument(arguments, 0, "source");
            ExpressionSyntax destination = argument(arguments, 1, "destination");
            string? sourceType = _inference.Resolve(source);
            string? destinationType = _inference.Resolve(destination);
            MapDefinition? definition = destinationType is not null ? registry.Resolve(destinationType, sourceType) : null;
            definition ??=
                sourceType is null ? null
                : registry.All.Where(m => TypeKey.Matches(m.SourceType, sourceType)).Take(2).ToList() is [MapDefinition only] ? only
                : null;
            if (definition is null)
                return (
                    null,
                    new UnresolvedCall(
                        invocation,
                        $"`{call}`: could not match the update mapping (source `{sourceType ?? "?"}`, destination `{destinationType ?? "?"}`); call `UpdateX(source, target)` on the mapper by hand."
                    )
                );
            if (statementOf(invocation, CallKind.Update) is null)
                return (
                    null,
                    new UnresolvedCall(
                        invocation,
                        $"`{call}`: `Map(source, destination)` used as an expression; Mapperly update methods return void, restructure the statement."
                    )
                );
            return (new ResolvedCall(invocation, CallKind.Update, definition, source, destination), null);
        }

        if (arguments.Count == 0)
            return (null, new UnresolvedCall(invocation, $"`{call}`: `Map` without a source argument."));

        ExpressionSyntax sourceExpression = argument(arguments, 0, "source");
        string destinationText = typeArguments.Arguments[^1].ToString();

        if (sourceExpression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
            return (new ResolvedCall(invocation, CallKind.Placeholder, null, sourceExpression, null, destinationText), null);

        string? sourceText =
            typeArguments.Arguments.Count == 2 ? typeArguments.Arguments[0].ToString() : _inference.Resolve(sourceExpression);

        if (SourceIndex.TryGetCollectionElementType(destinationText, out string destinationElement))
        {
            string? sourceElement =
                sourceText is not null && SourceIndex.TryGetCollectionElementType(sourceText, out string element) ? element : null;
            MapDefinition? collectionDefinition = registry.Resolve(destinationElement, sourceElement);
            if (collectionDefinition is null)
                return (null, new UnresolvedCall(invocation, $"`{call}`: no mapping found for the element type `{destinationElement}`."));
            return (new ResolvedCall(invocation, CallKind.Collection, collectionDefinition, sourceExpression, null), null);
        }

        MapDefinition? mapDefinition = registry.Resolve(destinationText, sourceText);
        if (mapDefinition is null)
        {
            int candidates = registry.FindByDestination(destinationText).Count;
            string reason =
                candidates == 0
                    ? $"`{call}`: no `CreateMap<…, {destinationText}>` found in any profile."
                    : $"`{call}`: {candidates} mappings target `{destinationText}` and the source type of the argument could not be inferred; call the right mapper method by hand.";
            return (null, new UnresolvedCall(invocation, reason));
        }
        return (new ResolvedCall(invocation, CallKind.Map, mapDefinition, sourceExpression, null), null);
    }

    private (ResolvedCall?, UnresolvedCall?) analyzeProjection(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax queryable,
        string destinationText
    )
    {
        string? queryableType = _inference.Resolve(queryable);
        string? sourceElement =
            queryableType is not null && SourceIndex.TryGetCollectionElementType(queryableType, out string element) ? element : null;
        MapDefinition? definition = registry.Resolve(destinationText, sourceElement);
        if (definition is null)
            return (
                null,
                new UnresolvedCall(
                    invocation,
                    $"`{singleLine(invocation)}`: no mapping found for `{destinationText}`; add `public static partial IQueryable<{destinationText}> ProjectTo…(this IQueryable<Source> q)` to the mapper."
                )
            );
        return (new ResolvedCall(invocation, CallKind.Projection, definition, queryable, null), null);
    }

    private static List<StatementSyntax> rewriteUpdateStatement(
        StatementSyntax statement,
        ExpressionSyntax replacement,
        ExpressionSyntax destination
    )
    {
        SyntaxTriviaList leading = statement.GetLeadingTrivia();
        SyntaxTriviaList trailing = statement.GetTrailingTrivia();
        StatementSyntax updateStatement = SyntaxFactory
            .ExpressionStatement(replacement)
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(trailing);
        SyntaxTriviaList indentation = leading
            .Where(t => !t.IsKind(SyntaxKind.SingleLineCommentTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia))
            .ToSyntaxTriviaList();
        string destinationName = stripSuppression(destination);

        switch (statement)
        {
            case LocalDeclarationStatementSyntax local when local.Declaration.Variables.Count == 1:
            {
                VariableDeclaratorSyntax declarator = local.Declaration.Variables[0];
                LocalDeclarationStatementSyntax assignment = local
                    .WithDeclaration(
                        local.Declaration.WithVariables(
                            SyntaxFactory.SingletonSeparatedList(
                                declarator.WithInitializer(SyntaxFactory.EqualsValueClause(destination.WithoutTrivia()))
                            )
                        )
                    )
                    .WithLeadingTrivia(indentation)
                    .WithTrailingTrivia(trailing);
                return [updateStatement, assignment];
            }
            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignmentExpression }:
            {
                if (stripSuppression(assignmentExpression.Left) == destinationName)
                    return [updateStatement];
                ExpressionStatementSyntax assignment = SyntaxFactory
                    .ExpressionStatement(assignmentExpression.WithRight(destination.WithoutTrivia()))
                    .WithLeadingTrivia(indentation)
                    .WithTrailingTrivia(trailing);
                return [updateStatement, assignment];
            }
            default:
                return [updateStatement];
        }
    }

    /// <summary>For update calls the whole statement is replaced; returns null for shapes that are rewritten inline.</summary>
    private static StatementSyntax? statementOf(InvocationExpressionSyntax invocation, CallKind kind)
    {
        if (kind != CallKind.Update)
            return null;
        SyntaxNode? parent = invocation.Parent;
        while (parent is PostfixUnaryExpressionSyntax or ParenthesizedExpressionSyntax)
            parent = parent.Parent;
        return parent switch
        {
            ExpressionStatementSyntax statement => statement,
            AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax statement } => statement,
            EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax statement } }
            } => statement,
            _ => null,
        };
    }

    private static CompilationUnitSyntax removeAutoMapperRegistration(CompilationUnitSyntax root, ref bool changed)
    {
        List<ExpressionStatementSyntax> registrations = root.DescendantNodes()
            .OfType<ExpressionStatementSyntax>()
            .Where(s =>
                s.Expression
                    is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddAutoMapper" } }
            )
            .ToList();
        if (registrations.Count == 0)
            return root;
        changed = true;
        return root.RemoveNodes(registrations, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    private CompilationUnitSyntax removeMapperPlumbing(string filePath, CompilationUnitSyntax root, ref bool changed)
    {
        // process innermost types first so nested handlers are cleaned before their declaring class is examined
        List<TypeDeclarationSyntax> types = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .OrderByDescending(t => t.Ancestors().OfType<TypeDeclarationSyntax>().Count())
            .ToList();
        root = root.TrackNodes(types);

        foreach (TypeDeclarationSyntax original in types)
        {
            TypeDeclarationSyntax? type = root.GetCurrentNode(original);
            if (type is null)
                continue;

            HashSet<string> names = mapperIdentifiers(type);
            if (names.Count == 0)
                continue;

            List<SyntaxNode> toRemove = [];
            foreach (FieldDeclarationSyntax field in type.Members.OfType<FieldDeclarationSyntax>())
                if (isMapperType(field.Declaration.Type))
                    toRemove.Add(field);
            foreach (PropertyDeclarationSyntax property in type.Members.OfType<PropertyDeclarationSyntax>())
                if (isMapperType(property.Type))
                    toRemove.Add(property);
            foreach (ParameterSyntax parameter in ownDescendants(type).OfType<ParameterSyntax>())
                if (parameter.Type is not null && isMapperType(parameter.Type))
                    toRemove.Add(parameter);
            foreach (LocalDeclarationStatementSyntax local in ownDescendants(type).OfType<LocalDeclarationStatementSyntax>())
                if (isMapperType(local.Declaration.Type))
                    toRemove.Add(local);
            foreach (ExpressionStatementSyntax statement in ownDescendants(type).OfType<ExpressionStatementSyntax>())
                if (
                    statement.Expression is AssignmentExpressionSyntax assignment
                    && isMapperName(assignment.Left, names)
                    && isMapperName(assignment.Right, names)
                )
                    toRemove.Add(statement);
            foreach (ArgumentSyntax argument in ownDescendants(type).OfType<ArgumentSyntax>())
                if (
                    argument.Expression is IdentifierNameSyntax identifier
                    && names.Contains(identifier.Identifier.Text)
                    && !isMapperDeclaredAsArgumentTarget(argument)
                )
                    toRemove.Add(argument);

            HashSet<SyntaxNode> removedSet = [.. toRemove];
            bool stillReferenced = ownDescendants(type)
                .OfType<IdentifierNameSyntax>()
                .Where(id => names.Contains(id.Identifier.Text))
                .Any(id => !id.Ancestors().Any(removedSet.Contains));
            if (stillReferenced)
            {
                report.Manual(
                    ManualWorkKind.AutoMapper,
                    filePath,
                    lineOf(type),
                    $"`{type.Identifier.Text}` still uses IMapper after rewriting the Map calls; finish it by hand (its mapper field/constructor parameter were kept)."
                );
                continue;
            }
            if (toRemove.Count == 0)
                continue;

            TypeDeclarationSyntax cleaned = type.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
            cleaned = tidyParameterLists(cleaned);
            root = root.ReplaceNode(type, cleaned);
            changed = true;
        }
        return root;
    }

    /// <summary>After removing the first parameter the next one keeps a stray leading space (<c>( IFoo foo</c>); drop it.</summary>
    private static TypeDeclarationSyntax tidyParameterLists(TypeDeclarationSyntax type)
    {
        return type.ReplaceNodes(
            type.DescendantNodes().OfType<ParameterListSyntax>().Where(needsTidy),
            (_, list) =>
            {
                ParameterSyntax first = list.Parameters[0];
                return list.ReplaceNode(first, first.WithLeadingTrivia(SyntaxTriviaList.Empty));
            }
        );

        static bool needsTidy(ParameterListSyntax list) =>
            list.Parameters.Count > 0
            && !list.OpenParenToken.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia))
            && list.Parameters[0].GetLeadingTrivia().All(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            && list.Parameters[0].GetLeadingTrivia().Count > 0;
    }

    private HashSet<string> mapperIdentifiers(TypeDeclarationSyntax type)
    {
        HashSet<string> names = [];
        foreach (FieldDeclarationSyntax field in type.Members.OfType<FieldDeclarationSyntax>())
            if (isMapperType(field.Declaration.Type))
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    names.Add(variable.Identifier.Text);
        foreach (PropertyDeclarationSyntax property in type.Members.OfType<PropertyDeclarationSyntax>())
            if (isMapperType(property.Type))
                names.Add(property.Identifier.Text);
        foreach (ParameterSyntax parameter in ownDescendants(type).OfType<ParameterSyntax>())
            if (parameter.Type is not null && isMapperType(parameter.Type))
                names.Add(parameter.Identifier.Text);
        foreach (
            VariableDeclarationSyntax declaration in ownDescendants(type)
                .OfType<LocalDeclarationStatementSyntax>()
                .Select(l => l.Declaration)
        )
            if (isMapperType(declaration.Type))
                foreach (VariableDeclaratorSyntax variable in declaration.Variables)
                    names.Add(variable.Identifier.Text);
        if (derivesFromMockRepository(type))
            names.Add("Mapper");
        return names;
    }

    /// <summary>Descendants of the type that are not inside a nested type declaration.</summary>
    private static IEnumerable<SyntaxNode> ownDescendants(TypeDeclarationSyntax type) =>
        type.DescendantNodes(descendIntoChildren: node => ReferenceEquals(node, type) || node is not TypeDeclarationSyntax)
            .Where(n => n is not TypeDeclarationSyntax);

    private bool derivesFromMockRepository(TypeDeclarationSyntax type)
    {
        HashSet<string> visited = [];
        return check(type.BaseList?.Types.Select(t => t.Type.ToString()) ?? []);

        bool check(IEnumerable<string> baseTypes)
        {
            foreach (string baseType in baseTypes)
            {
                string simple = TypeKey.Simple(baseType);
                if (simple.EndsWith("MockRepository", StringComparison.Ordinal))
                    return true;
                if (!visited.Add(simple))
                    continue;
                SourceType? resolved = index.Find(simple);
                if (resolved is not null && check(resolved.BaseTypes))
                    return true;
            }
            return false;
        }
    }

    private static bool isMapperType(TypeSyntax type) =>
        type.ToString().TrimEnd('?') is "IMapper" or "AutoMapper.IMapper" or "Mapper" or "AutoMapper.Mapper";

    private static bool isMapperReceiver(ExpressionSyntax receiver, HashSet<string> names) =>
        receiver switch
        {
            IdentifierNameSyntax identifier => names.Contains(identifier.Identifier.Text),
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => names.Contains(member.Name.Identifier.Text),
            _ => false,
        };

    private static bool isMapperName(ExpressionSyntax expression, HashSet<string> names) => isMapperReceiver(expression, names);

    private static bool mentionsMapper(ExpressionSyntax expression, HashSet<string> names) =>
        expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => names.Contains(id.Identifier.Text));

    /// <summary><c>Mapper</c> inside <c>new MapperConfiguration(...)</c> is not a plain argument to drop.</summary>
    private static bool isMapperDeclaredAsArgumentTarget(ArgumentSyntax argument) =>
        argument.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any(c => c.Type.ToString().Contains("MapperConfiguration"));

    private static ExpressionSyntax argument(SeparatedSyntaxList<ArgumentSyntax> arguments, int position, string name)
    {
        ArgumentSyntax? named = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        return (named ?? arguments.Where(a => a.NameColon is null).ElementAtOrDefault(position) ?? arguments[position]).Expression;
    }

    private static string stripSuppression(ExpressionSyntax expression) =>
        expression is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } postfix
            ? postfix.Operand.ToString().Trim()
            : expression.ToString().Trim();

    private static readonly HashSet<string> _autoMapperTypeNames =
    [
        "IMapper",
        "Mapper",
        "Profile",
        "MapperConfiguration",
        "IMappingAction",
        "ITypeConverter",
        "IValueResolver",
        "IMemberValueResolver",
        "IConfigurationProvider",
        "ProjectTo",
        "AutoMapper",
    ];

    /// <summary>Looks at identifiers only, so a mention in a comment does not keep <c>using AutoMapper;</c> alive.</summary>
    private static bool stillUsesAutoMapper(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .Where(n => n is not UsingDirectiveSyntax && !n.Ancestors().OfType<UsingDirectiveSyntax>().Any())
            .OfType<SimpleNameSyntax>()
            .Any(n => _autoMapperTypeNames.Contains(n.Identifier.Text));

    private static string singleLine(SyntaxNode node) =>
        string.Join(' ', node.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    private static int lineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
