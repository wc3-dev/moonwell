using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moonwell.Analyzers;

/// <summary>
/// MW0058 (decision 374): keeps a map's build code, the files in its <c>buildtime</c> folder, and its game code from naming
/// each other's types. The build's own two compiles stop on a crossing; this shows it in the editor, where both are one project.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BuildTimeAnalyzer : DiagnosticAnalyzer
{
    private const string Library = "Moonwell.BuildTime";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.BuildTime;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Check, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
    }

    private static void Check(SyntaxNodeAnalysisContext context)
    {
        // var names no type in the code: the type it stands for is reported where it is written.
        if (!GameCode.HasGameCode(context.Compilation, context.Options) || context.Node is IdentifierNameSyntax { IsVar: true } || context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol type)
        {
            return;
        }

        var options = context.Options;
        var declared = type.OriginalDefinition.DeclaringSyntaxReferences;
        var assembly = type.ContainingAssembly?.Name ?? string.Empty;
        if (GameCode.IsBuildCode(context.Node.SyntaxTree, options))
        {
            var game = declared.Length > 0
                ? declared.Any(one => !GameCode.IsBuildCode(one.SyntaxTree, options))
                : assembly == "Moonwell" || (assembly.StartsWith("Moonwell.", StringComparison.Ordinal) && assembly != Library);
            if (game)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.BuildTimeLine, context.Node.GetLocation(), type.Name, "game code, which is not compiled yet when build code runs: build code reads the map's files and gives its results"));
            }
        }
        else
        {
            var build = declared.Length > 0
                ? declared.All(one => GameCode.IsBuildCode(one.SyntaxTree, options))
                : assembly == Library || assembly.StartsWith("War3Net.", StringComparison.Ordinal);
            if (build)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.BuildTimeLine, context.Node.GetLocation(), type.Name, "build code, which never reaches the game: game code reads what build code gives from Data"));
            }
        }
    }
}
