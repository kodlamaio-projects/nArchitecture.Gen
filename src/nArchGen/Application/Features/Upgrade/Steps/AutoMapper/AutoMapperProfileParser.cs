using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps.AutoMapper;

/// <summary>Turns AutoMapper <c>Profile</c> classes into <see cref="MapDefinition"/>s by walking the <c>CreateMap(...)...</c> call chains.</summary>
public static class AutoMapperProfileParser
{
    private static readonly string[] _unsupportedCalls =
    [
        "ConstructUsing",
        "ConvertUsing",
        "BeforeMap",
        "AfterMap",
        "IncludeBase",
        "IncludeDerived",
        "Include",
        "IncludeMembers",
        "IncludeAllDerived",
        "ForAllMembers",
        "ForAllOtherMembers",
        "ForCtorParam",
        "MaxDepth",
        "PreserveReferences",
        "ValidateMemberList",
        "DisableCtorValidation",
        "AddTransform",
        "ForMember",
        "ForPath",
    ];

    public static MapRegistry Parse(UpgradeContext context)
    {
        MapRegistry registry = new();
        foreach (string file in context.ProjectAndTestCSharpFiles)
        {
            string text = context.Files.Read(file);
            if (!text.Contains("Profile"))
                continue;

            CompilationUnitSyntax root = context.Files.GetTree(file).GetCompilationUnitRoot();
            foreach (ClassDeclarationSyntax declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!isProfile(declaration))
                    continue;

                ProfileInfo profile = new()
                {
                    ClassName = declaration.Identifier.Text,
                    Namespace =
                        declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(n => n.Name.ToString()).FirstOrDefault()
                        ?? string.Empty,
                    FilePath = file,
                    Declaration = declaration,
                    UsingDirectives = root
                        .Usings.Select(u => u.ToString().Trim())
                        .Where(u => !u.StartsWith("using AutoMapper", StringComparison.Ordinal))
                        .ToList(),
                };
                parseMaps(profile);
                registry.Profiles.Add(profile);
            }
        }
        return registry;
    }

    private static bool isProfile(ClassDeclarationSyntax declaration) =>
        declaration.BaseList?.Types.Any(t => t.Type.ToString() is "Profile" or "AutoMapper.Profile") == true;

    private static void parseMaps(ProfileInfo profile)
    {
        foreach (ExpressionStatementSyntax statement in profile.Declaration.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (statement.Expression is not InvocationExpressionSyntax outermost)
                continue;

            List<InvocationExpressionSyntax> chain = [];
            InvocationExpressionSyntax? createMap = null;
            bool nonGeneric = false;
            ExpressionSyntax current = outermost;
            while (current is InvocationExpressionSyntax invocation)
            {
                switch (invocation.Expression)
                {
                    case MemberAccessExpressionSyntax memberAccess when memberAccess.Name.Identifier.Text == "CreateMap":
                        createMap = invocation;
                        nonGeneric = memberAccess.Name is not GenericNameSyntax;
                        current = memberAccess.Expression;
                        break;
                    case MemberAccessExpressionSyntax memberAccess:
                        chain.Add(invocation);
                        current = memberAccess.Expression;
                        continue;
                    case GenericNameSyntax { Identifier.Text: "CreateMap" }:
                        createMap = invocation;
                        break;
                    case IdentifierNameSyntax { Identifier.Text: "CreateMap" }:
                        createMap = invocation;
                        nonGeneric = true;
                        break;
                }
                break;
            }

            if (createMap is null)
                continue;
            int line = lineOf(statement);
            if (nonGeneric)
            {
                profile.UnconvertibleCalls.Add(
                    (
                        line,
                        $"`{firstLine(statement)}`: non-generic CreateMap(typeof(...)) cannot be translated; declare the mapper method by hand."
                    )
                );
                continue;
            }

            GenericNameSyntax generic = createMap.Expression switch
            {
                GenericNameSyntax g => g,
                MemberAccessExpressionSyntax { Name: GenericNameSyntax g } => g,
                _ => throw new InvalidOperationException("unreachable"),
            };
            if (generic.TypeArgumentList.Arguments.Count != 2)
                continue;

            string source = generic.TypeArgumentList.Arguments[0].ToString();
            string destination = generic.TypeArgumentList.Arguments[1].ToString();
            MapDefinition forward = getOrAdd(profile, source, destination, isReverse: false, line);
            MapDefinition active = forward;

            chain.Reverse();
            foreach (InvocationExpressionSyntax call in chain)
            {
                string name = ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.Text;
                switch (name)
                {
                    case "ReverseMap":
                        MapDefinition reverse = getOrAdd(profile, destination, source, isReverse: true, line);
                        forward.Counterpart = reverse;
                        reverse.Counterpart = forward;
                        active = reverse;
                        break;
                    case "ForMember":
                    case "ForPath":
                        active.Members.Add(parseForMember(call));
                        break;
                    case "ForSourceMember":
                        string? sourceMember = memberPath(call.ArgumentList.Arguments.ElementAtOrDefault(0)?.Expression);
                        active.Members.Add(
                            sourceMember is null
                                ? new MemberMapping(MemberMappingKind.Unsupported, null, null, singleLine(call))
                                : new MemberMapping(MemberMappingKind.IgnoreSource, null, sourceMember, singleLine(call))
                        );
                        break;
                    default:
                        active.Members.Add(new MemberMapping(MemberMappingKind.Unsupported, null, null, singleLine(call)));
                        break;
                }
            }
        }
    }

    private static MapDefinition getOrAdd(ProfileInfo profile, string source, string destination, bool isReverse, int line)
    {
        MapDefinition? existing = profile.Maps.FirstOrDefault(m =>
            TypeKey.Matches(m.SourceType, source) && TypeKey.Matches(m.DestinationType, destination)
        );
        if (existing is not null)
            return existing;
        MapDefinition definition = new()
        {
            Profile = profile,
            SourceType = source,
            DestinationType = destination,
            IsReverse = isReverse,
            Line = line,
        };
        profile.Maps.Add(definition);
        return definition;
    }

    private static MemberMapping parseForMember(InvocationExpressionSyntax call)
    {
        string raw = singleLine(call);
        SeparatedSyntaxList<ArgumentSyntax> args = call.ArgumentList.Arguments;
        if (args.Count != 2)
            return new MemberMapping(MemberMappingKind.Unsupported, null, null, raw);

        string? target = memberPath(args[0].Expression);
        if (target is null)
            return new MemberMapping(MemberMappingKind.Unsupported, null, null, raw);

        if (args[1].Expression is not LambdaExpressionSyntax optionLambda || optionLambda.Body is not InvocationExpressionSyntax optionCall)
            return new MemberMapping(MemberMappingKind.Unsupported, target, null, raw);

        string option = (optionCall.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text ?? string.Empty;
        switch (option)
        {
            case "Ignore":
                return new MemberMapping(MemberMappingKind.IgnoreTarget, target, null, raw);
            case "MapFrom" when optionCall.ArgumentList.Arguments.Count == 1:
                string? sourcePath = memberPath(optionCall.ArgumentList.Arguments[0].Expression);
                return sourcePath is null
                    ? new MemberMapping(MemberMappingKind.Unsupported, target, null, raw)
                    : new MemberMapping(MemberMappingKind.MapFromPath, target, sourcePath, raw);
            default:
                return new MemberMapping(MemberMappingKind.Unsupported, target, null, raw);
        }
    }

    /// <summary>Returns <c>A.B</c> for a lambda <c>x => x.A.B</c>; null for anything that is not a plain member path.</summary>
    private static string? memberPath(ExpressionSyntax? expression)
    {
        if (expression is not LambdaExpressionSyntax lambda)
            return null;
        string parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized
                .ParameterList
                .Parameters[0]
                .Identifier
                .Text,
            _ => string.Empty,
        };
        if (parameter.Length == 0)
            return null;

        List<string> segments = [];
        ExpressionSyntax? current = lambda.Body as ExpressionSyntax;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            segments.Insert(0, memberAccess.Name.Identifier.Text);
            current = memberAccess.Expression;
        }
        if (current is PostfixUnaryExpressionSyntax postfix)
            current = postfix.Operand;
        return current is IdentifierNameSyntax identifier && identifier.Identifier.Text == parameter && segments.Count > 0
            ? string.Join('.', segments)
            : null;
    }

    private static string singleLine(SyntaxNode node)
    {
        MemberAccessExpressionSyntax? access = (node as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;
        string text = access is not null ? $".{access.Name}{((InvocationExpressionSyntax)node).ArgumentList}" : node.ToString();
        return string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
    }

    private static string firstLine(SyntaxNode node) => node.ToString().Split('\n')[0].Trim();

    private static int lineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
