using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moonwell.Analyzers;

/// <summary>
/// Tells a map's game code from its build code, the files in the project's <c>buildtime</c> folder (decision 371), which runs
/// on .NET while the map builds and never reaches the game. The rules about the game and its Lua register their actions
/// through the methods here, so they leave build code alone: there an unseeded <c>Random</c>, a local named <c>end</c> or
/// a form CSharp.lua cannot compile is nothing wrong. Only <see cref="BuildTimeAnalyzer"/> looks at both.
/// </summary>
internal static class GameCode
{
    private const string Folder = "buildtime";

    /// <summary>Whether a file is build code: in the project's buildtime folder, or, with no project folder known, in any folder of that name.</summary>
    public static bool IsBuildCode(SyntaxTree tree, AnalyzerOptions options)
    {
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.projectdir", out var projectFolder);
        var parts = tree.FilePath.Split('/', '\\');
        if (!string.IsNullOrEmpty(projectFolder))
        {
            var root = projectFolder!.Split('/', '\\').Where(part => part.Length > 0).ToArray();
            if (parts.Length > root.Length + 1 && root.Zip(parts, (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)).All(same => same))
            {
                return string.Equals(parts[root.Length], Folder, StringComparison.OrdinalIgnoreCase);
            }
        }

        return parts.Take(parts.Length - 1).Any(part => string.Equals(part, Folder, StringComparison.OrdinalIgnoreCase));
    }

    public static void RegisterGameOperationAction(this AnalysisContext context, Action<OperationAnalysisContext> action, params OperationKind[] kinds) =>
        context.RegisterOperationAction(Game(action), kinds);

    public static void RegisterGameOperationAction(this CompilationStartAnalysisContext context, Action<OperationAnalysisContext> action, params OperationKind[] kinds) =>
        context.RegisterOperationAction(Game(action), kinds);

    public static void RegisterGameOperationAction(this CompilationStartAnalysisContext context, Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> kinds) =>
        context.RegisterOperationAction(Game(action), kinds);

    public static void RegisterGameSyntaxNodeAction(this AnalysisContext context, Action<SyntaxNodeAnalysisContext> action, params SyntaxKind[] kinds) =>
        context.RegisterSyntaxNodeAction(Game(action), kinds);

    public static void RegisterGameSyntaxNodeAction(this CompilationStartAnalysisContext context, Action<SyntaxNodeAnalysisContext> action, params SyntaxKind[] kinds) =>
        context.RegisterSyntaxNodeAction(Game(action), kinds);

    public static void RegisterGameSymbolAction(this AnalysisContext context, Action<SymbolAnalysisContext> action, params SymbolKind[] kinds) =>
        context.RegisterSymbolAction(Game(action), kinds);

    public static void RegisterGameSymbolAction(this CompilationStartAnalysisContext context, Action<SymbolAnalysisContext> action, params SymbolKind[] kinds) =>
        context.RegisterSymbolAction(Game(action), kinds);

    private static Action<OperationAnalysisContext> Game(Action<OperationAnalysisContext> action) => context =>
    {
        if (!IsBuildCode(context.Operation.Syntax.SyntaxTree, context.Options))
        {
            action(context);
        }
    };

    private static Action<SyntaxNodeAnalysisContext> Game(Action<SyntaxNodeAnalysisContext> action) => context =>
    {
        if (!IsBuildCode(context.Node.SyntaxTree, context.Options))
        {
            action(context);
        }
    };

    // A symbol is build code's when every place that declares it is: a partial class with one part outside is game code.
    private static Action<SymbolAnalysisContext> Game(Action<SymbolAnalysisContext> action) => context =>
    {
        var declared = context.Symbol.DeclaringSyntaxReferences;
        var options = context.Options;
        if (declared.Length == 0 || declared.Any(one => !IsBuildCode(one.SyntaxTree, options)))
        {
            action(context);
        }
    };
}
