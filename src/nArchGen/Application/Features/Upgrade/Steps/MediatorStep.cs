using System.Text.RegularExpressions;
using Core.CodeGen.Code.CSharp.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Application.Features.Upgrade.Steps;

/// <summary>MediatR → NArchitecture.Core.Mediator: namespaces and the registration call. Notifications are reported.</summary>
public sealed partial class MediatorStep : IUpgradeStep
{
    private const string _mediatorNamespace = "NArchitecture.Core.Mediator";
    private const string _mediatorDiNamespace = "NArchitecture.Core.Mediator.DependencyInjection";

    public string Id => "mediator";
    public string Title => "Replacing MediatR with NArchitecture.Core.Mediator";

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        int usingCount = 0,
            registrationCount = 0;

        foreach (string file in context.ProjectAndTestCSharpFiles.ToList())
        {
            string text = context.Files.Read(file);
            if (!text.Contains("MediatR"))
                continue;

            CompilationUnitSyntax root = context.Files.GetTree(file).GetCompilationUnitRoot();
            bool changed = false;

            if (UsingDirectiveHelper.HasUsing(root, "MediatR"))
            {
                root = UsingDirectiveHelper.ReplaceUsing(root, "MediatR", _mediatorNamespace);
                usingCount++;
                changed = true;
            }
            foreach (
                UsingDirectiveSyntax other in root
                    .Usings.Where(u => u.Name?.ToString().StartsWith("MediatR.", StringComparison.Ordinal) == true)
                    .ToList()
            )
                context.Report.Manual(
                    ManualWorkKind.Mediator,
                    file,
                    lineOf(other),
                    $"`{other}` has no NArchitecture.Core.Mediator counterpart; remove it and adapt the code that needs it."
                );

            if (root.ToFullString().Contains("AddMediatR("))
            {
                string updated = root.ToFullString().Replace("AddMediatR(", "AddMediator(");
                root = CSharpSyntaxTree.ParseText(updated, path: file).GetCompilationUnitRoot();
                root = UsingDirectiveHelper.AddUsing(root, _mediatorDiNamespace);
                registrationCount++;
                changed = true;
            }

            foreach (Match match in NotificationRegex().Matches(root.ToFullString()))
            {
                int line = root.SyntaxTree.GetLineSpan(new TextSpan(match.Index, match.Length)).StartLinePosition.Line + 1;
                context.Report.Manual(
                    ManualWorkKind.Mediator,
                    file,
                    line,
                    $"`{match.Value.Trim()}`: NArchitecture.Core.Mediator has no notifications. Keep a small in-house publisher or keep MediatR for notifications only."
                );
            }

            foreach (Match match in QualifiedRegex().Matches(root.ToFullString()))
            {
                int line = root.SyntaxTree.GetLineSpan(new TextSpan(match.Index, match.Length)).StartLinePosition.Line + 1;
                context.Report.Manual(
                    ManualWorkKind.Mediator,
                    file,
                    line,
                    $"Fully qualified `{match.Value}` reference; rename to `NArchitecture.Core.Mediator`."
                );
            }

            if (changed)
                context.Files.Write(file, root.ToFullString());
        }

        if (usingCount > 0)
            context.Report.Add(Title, $"`using MediatR;` → `using {_mediatorNamespace};` in {usingCount} file(s)");
        if (registrationCount > 0)
            context.Report.Add(Title, $"`AddMediatR(...)` → `AddMediator(...)` in {registrationCount} file(s)");
        return Task.CompletedTask;
    }

    private static int lineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    [GeneratedRegex(@"\bINotificationHandler\b|\bINotification\b|\bIPublisher\b|\.Publish\s*\(")]
    private static partial Regex NotificationRegex();

    [GeneratedRegex(@"\bMediatR\.(?!DependencyInjection)[A-Z]\w*")]
    private static partial Regex QualifiedRegex();
}
