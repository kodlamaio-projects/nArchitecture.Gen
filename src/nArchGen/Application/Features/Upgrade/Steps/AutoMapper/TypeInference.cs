using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Application.Features.Upgrade.Steps.AutoMapper;

/// <summary>
/// Best-effort, syntax-only type resolution for the expressions that end up as <c>_mapper.Map&lt;T&gt;(expr)</c> arguments:
/// parameters, locals (including <c>var</c> initialised from <c>new</c>, repositories or same-class methods), fields,
/// properties and member paths over types known to the <see cref="SourceIndex"/>.
/// </summary>
public sealed class TypeInference(SourceIndex index)
{
    private static readonly string[] _repositoryBases = ["IAsyncRepository", "IRepository", "EfRepositoryBase", "IQuery"];

    public string? Resolve(ExpressionSyntax expression, int depth = 0)
    {
        if (depth > 8)
            return null;

        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return Resolve(parenthesized.Expression, depth + 1);
            case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } postfix:
                return Resolve(postfix.Operand, depth + 1);
            case AwaitExpressionSyntax awaitExpression:
                return unwrapTask(Resolve(awaitExpression.Expression, depth + 1));
            case CastExpressionSyntax cast:
                return cast.Type.ToString();
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression:
                return asExpression.Right.ToString();
            case ObjectCreationExpressionSyntax creation:
                return creation.Type.ToString();
            case ThisExpressionSyntax thisExpression:
                return containingTypeName(thisExpression);
            case IdentifierNameSyntax identifier:
                return resolveIdentifier(identifier, depth);
            case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } thisMember:
                return resolveMemberOfType(containingTypeName(thisMember), thisMember.Name.Identifier.Text);
            case MemberAccessExpressionSyntax memberAccess:
                string? receiver = Resolve(memberAccess.Expression, depth + 1);
                return receiver is null ? null : resolveMemberOfType(receiver, memberAccess.Name.Identifier.Text);
            case ElementAccessExpressionSyntax elementAccess:
                string? collection = Resolve(elementAccess.Expression, depth + 1);
                return collection is not null && SourceIndex.TryGetCollectionElementType(collection, out string element) ? element : null;
            case InvocationExpressionSyntax invocation:
                return resolveInvocation(invocation, depth);
            case ConditionalExpressionSyntax conditional:
                return Resolve(conditional.WhenTrue, depth + 1) ?? Resolve(conditional.WhenFalse, depth + 1);
            default:
                return null;
        }
    }

    private string? resolveIdentifier(IdentifierNameSyntax identifier, int depth)
    {
        string name = identifier.Identifier.Text;
        SyntaxNode? scope = identifier
            .Ancestors()
            .FirstOrDefault(a =>
                a is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax
            );

        foreach (SyntaxNode ancestor in identifier.Ancestors())
        {
            ParameterListSyntax? parameters = ancestor switch
            {
                BaseMethodDeclarationSyntax method => method.ParameterList,
                LocalFunctionStatementSyntax local => local.ParameterList,
                ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList,
                _ => null,
            };
            ParameterSyntax? parameter = parameters?.Parameters.FirstOrDefault(p => p.Identifier.Text == name);
            if (parameter?.Type is not null)
                return parameter.Type.ToString();
            if (ancestor is TypeDeclarationSyntax)
                break;
        }

        if (scope is not null)
        {
            foreach (
                VariableDeclaratorSyntax declarator in scope
                    .DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Where(d => d.Identifier.Text == name)
            )
            {
                if (declarator.Parent is not VariableDeclarationSyntax declaration)
                    continue;
                if (!declaration.Type.IsVar)
                    return declaration.Type.ToString();
                if (declarator.Initializer is not null)
                    return Resolve(declarator.Initializer.Value, depth + 1);
            }
            foreach (
                ForEachStatementSyntax forEach in scope
                    .DescendantNodes()
                    .OfType<ForEachStatementSyntax>()
                    .Where(f => f.Identifier.Text == name)
            )
            {
                if (!forEach.Type.IsVar)
                    return forEach.Type.ToString();
                string? collection = Resolve(forEach.Expression, depth + 1);
                if (collection is not null && SourceIndex.TryGetCollectionElementType(collection, out string element))
                    return element;
            }
            foreach (DeclarationPatternSyntax pattern in scope.DescendantNodes().OfType<DeclarationPatternSyntax>())
                if (pattern.Designation is SingleVariableDesignationSyntax designation && designation.Identifier.Text == name)
                    return pattern.Type.ToString();
            foreach (DeclarationExpressionSyntax declarationExpression in scope.DescendantNodes().OfType<DeclarationExpressionSyntax>())
                if (
                    declarationExpression.Designation is SingleVariableDesignationSyntax designation
                    && designation.Identifier.Text == name
                    && !declarationExpression.Type.IsVar
                )
                    return declarationExpression.Type.ToString();
        }

        string? containing = containingTypeName(identifier);
        return containing is null ? null : resolveMemberOfType(containing, name);
    }

    private string? resolveInvocation(InvocationExpressionSyntax invocation, int depth)
    {
        string methodName;
        string? receiverType;
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                methodName = memberAccess.Name.Identifier.Text;
                receiverType =
                    memberAccess.Expression is ThisExpressionSyntax
                        ? containingTypeName(invocation)
                        : Resolve(memberAccess.Expression, depth + 1);
                break;
            case IdentifierNameSyntax identifier:
                methodName = identifier.Identifier.Text;
                receiverType = containingTypeName(invocation);
                break;
            case GenericNameSyntax generic:
                methodName = generic.Identifier.Text;
                receiverType = containingTypeName(invocation);
                break;
            default:
                return null;
        }
        if (receiverType is null)
            return null;

        SourceType? receiver = index.Find(receiverType);
        if (receiver is not null)
        {
            MethodDeclarationSyntax? method = receiver
                .Declaration.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);
            if (method is not null)
                return unwrapTask(method.ReturnType.ToString());
        }

        string? entity = repositoryEntity(receiverType, receiver);
        if (entity is null)
            return null;
        return methodName switch
        {
            "GetListAsync" or "GetListByDynamicAsync" or "GetList" or "GetListByDynamic" => $"IPaginate<{entity}>",
            "GetAsync" or "Get" or "AddAsync" or "Add" or "UpdateAsync" or "Update" or "DeleteAsync" or "Delete" => entity,
            "AddRangeAsync" or "AddRange" or "UpdateRangeAsync" or "UpdateRange" or "DeleteRangeAsync" or "DeleteRange" =>
                $"ICollection<{entity}>",
            _ => null,
        };
    }

    /// <summary>Entity type of a repository (<c>IBrandRepository : IAsyncRepository&lt;Brand, Guid&gt;</c>).</summary>
    private string? repositoryEntity(string receiverType, SourceType? receiver)
    {
        string simple = TypeKey.Simple(receiverType);
        if (_repositoryBases.Contains(simple))
        {
            string[] arguments = TypeKey.GenericArguments(receiverType).Split(',');
            return arguments.Length > 0 && arguments[0].Length > 0 ? arguments[0] : null;
        }
        if (receiver is null)
            return null;
        foreach (string baseType in receiver.BaseTypes)
        {
            string? entity = repositoryEntity(baseType, index.Find(baseType));
            if (entity is not null)
                return entity;
        }
        return null;
    }

    private string? resolveMemberOfType(string? typeText, string memberName)
    {
        if (typeText is null)
            return null;
        SourceType? type = index.Find(typeText);
        if (type is null)
            return null;
        return index.GetAllMembers(type).FirstOrDefault(m => m.Name == memberName)?.TypeText;
    }

    private static string? containingTypeName(SyntaxNode node) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;

    private static string? unwrapTask(string? typeText)
    {
        if (typeText is null)
            return null;
        string text = TypeKey.Normalize(typeText);
        string simple = TypeKey.Simple(text);
        if (simple is "Task" or "ValueTask")
        {
            string arguments = TypeKey.GenericArguments(text);
            return arguments.Length == 0 ? null : arguments;
        }
        return typeText;
    }
}
