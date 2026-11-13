using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Moonwell.Analyzers;

/// <summary>
/// MW0057 (J17): a native the game crashes on, given one of its Strings or Textures found by name
/// (<c>BlzGetFrameByName("SimpleNameValue", 0)</c>), straight or through a local or field that holds one.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FramePartAnalyzer : DiagnosticAnalyzer
{
    // Tasyen's eight, and BlzFrameGetParent, seen crashing on InfoPanelIconHeroStrengthValue and SimpleNameValue.
    private static readonly HashSet<string> Crashing = new()
    {
        "BlzFrameSetVisible", "BlzFrameIsVisible", "BlzFrameSetAlpha", "BlzFrameSetLevel", "BlzFrameSetEnable",
        "BlzFrameSetScale", "BlzFrameSetParent", "BlzDestroyFrame", "BlzFrameGetParent",
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.FrameParts;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // A field may be set in one file and used in another, so the calls on a local or field are reported at the end.
            var holds = new ConcurrentDictionary<ISymbol, string>(SymbolEqualityComparer.Default);
            var calls = new ConcurrentBag<(IInvocationOperation Call, ISymbol Held)>();
            start.RegisterOperationAction(operation => Called(operation, calls), OperationKind.Invocation);
            start.RegisterOperationAction(operation => Kept(operation, holds),
                OperationKind.SimpleAssignment, OperationKind.VariableDeclarator, OperationKind.FieldInitializer);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var (call, held) in calls)
                {
                    if (holds.TryGetValue(held, out var name))
                    {
                        end.ReportDiagnostic(Report(call, name));
                    }
                }
            });
        });
    }

    private static void Called(OperationAnalysisContext context, ConcurrentBag<(IInvocationOperation, ISymbol)> calls)
    {
        var call = (IInvocationOperation)context.Operation;
        if (!IsNative(call.TargetMethod) || !Crashing.Contains(call.TargetMethod.Name) || call.Arguments.Length == 0)
        {
            return;
        }

        var frame = Unwrapped(call.Arguments[0].Value);
        if (Found(frame) is { } name)
        {
            context.ReportDiagnostic(Report(call, name));
        }
        else if (frame is ILocalReferenceOperation local)
        {
            calls.Add((call, local.Local));
        }
        else if (frame is IFieldReferenceOperation field)
        {
            calls.Add((call, field.Field.OriginalDefinition));
        }
    }

    // What a local or field is given: a String or Texture found by name marks it.
    private static void Kept(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, string> holds)
    {
        switch (context.Operation)
        {
            case ISimpleAssignmentOperation assignment when Found(Unwrapped(assignment.Value)) is { } name:
                if (Unwrapped(assignment.Target) is ILocalReferenceOperation local)
                {
                    holds[local.Local] = name;
                }
                else if (Unwrapped(assignment.Target) is IFieldReferenceOperation field)
                {
                    holds[field.Field.OriginalDefinition] = name;
                }

                break;
            case IVariableDeclaratorOperation { Initializer: { } initializer } declarator when Found(Unwrapped(initializer.Value)) is { } name:
                holds[declarator.Symbol] = name;
                break;
            case IFieldInitializerOperation initializer when Found(Unwrapped(initializer.Value)) is { } name:
                foreach (var field in initializer.InitializedFields)
                {
                    holds[field.OriginalDefinition] = name;
                }

                break;
        }
    }

    // The String's or Texture's name, when this is BlzGetFrameByName of one written as text.
    private static string? Found(IOperation operation)
    {
        return operation is IInvocationOperation { TargetMethod.Name: "BlzGetFrameByName", Arguments.Length: > 0 } call
            && IsNative(call.TargetMethod)
            && call.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string name }
            && GameFrameParts.All.ContainsKey(name)
                ? name
                : null;
    }

    private static Diagnostic Report(IInvocationOperation call, string name)
    {
        var (kind, frame) = GameFrameParts.All[name];
        var instead = call.TargetMethod.Name == "BlzFrameGetParent"
            ? frame != null ? $"find the frame it sits in by that frame's own name, BlzGetFrameByName(\"{frame}\", 0)" : "find the frame it sits in by that frame's own name, which the game's .fdf files give"
            : "move it off with BlzFrameClearAllPoints and BlzFrameSetAbsPoint, which take one with no crash" + (frame != null ? $", or call it on the frame it sits in, '{frame}'" : "");
        return Diagnostic.Create(Rules.FramePart, call.Syntax.GetLocation(), call.Syntax.ToString(), name, kind, instead);
    }

    private static IOperation Unwrapped(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsNative(IMethodSymbol method)
    {
        return method.ContainingType is { Name: "Natives", ContainingNamespace: { Name: "Moonwell", ContainingNamespace.IsGlobalNamespace: true } };
    }
}
