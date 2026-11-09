using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moonwell.Analyzers;

/// <summary>
/// Warns of <c>Remove&lt;T&gt;()</c> in an item's or item component's <c>OnDropped</c> or <c>OnUnequipped</c> (MW0055, decision
/// 216): it takes the first component of the class on the actor, whichever item added it, where one given with
/// <c>heldBy: Item.Carry</c> ends with this item's carry.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ItemRemoveAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.ItemRemoves;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Call, SyntaxKind.InvocationExpression);
    }

    private static void Call(SyntaxNodeAnalysisContext context)
    {
        var call = (InvocationExpressionSyntax)context.Node;
        if (call.ArgumentList.Arguments.Count != 0
            || context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is not IMethodSymbol { Name: "Remove", TypeArguments.Length: 1 } remove
            || !IsMoonwell(remove.ContainingType, "Actor"))
        {
            return;
        }

        var method = call.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var name = method?.Identifier.ValueText;
        if (method == null || (name != "OnDropped" && name != "OnUnequipped") || !method.Modifiers.Any(SyntaxKind.OverrideKeyword))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken)?.ContainingType is { } type && IsItemSide(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.ItemRemove, call.GetLocation(), call.ToString(), name, remove.TypeArguments[0].Name));
        }
    }

    // An item class or an item component class.
    private static bool IsItemSide(INamedTypeSymbol type)
    {
        for (var parent = (INamedTypeSymbol?)type; parent != null; parent = parent.BaseType)
        {
            if (IsMoonwell(parent, "Item") || IsMoonwell(parent, "ItemComponent"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMoonwell(INamedTypeSymbol type, string name)
    {
        return type.Name == name && type.ContainingNamespace is { Name: "Moonwell", ContainingNamespace.IsGlobalNamespace: true };
    }
}
