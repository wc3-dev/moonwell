using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Moonwell.Analyzers;

/// <summary>
/// MW0051: a buff class a spell lists (an ability class's <c>Buffs.Set</c>, <c>Add</c> or <c>Replace</c> names it, or the
/// class is <c>[Replaces]</c> of a standard buff) is the game's to put on, and Moonwell watches for it (decisions 152 and
/// 154). It cannot also go through <c>Apply</c>; it does not set <c>Duration</c>, <c>Stacking</c> or <c>EndsOnDeath</c>,
/// which the game decides; and it has a constructor with no parameters, as Moonwell makes it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpellBuffAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] GameDecides = { "Duration", "Stacking", "EndsOnDeath" };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.SpellBuffs;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Which classes a spell lists is known only once every file is seen, so what could break the rule is gathered, and
            // reported at the end for the classes listed.
            var listed = new ConcurrentDictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
            var found = new ConcurrentBag<(INamedTypeSymbol Type, Location Location, string Message)>();
            var set = new ConcurrentBag<(INamedTypeSymbol Holder, Location Location, string Property)>();
            start.RegisterOperationAction(operation => Called(operation, listed, found), OperationKind.Invocation);
            start.RegisterOperationAction(operation => Assigned(operation, set),
                OperationKind.SimpleAssignment, OperationKind.CompoundAssignment, OperationKind.CoalesceAssignment, OperationKind.DeconstructionAssignment);
            start.RegisterSymbolAction(symbol => Declared(symbol, listed, found), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var one in found)
                {
                    if (listed.ContainsKey(one.Type))
                    {
                        end.ReportDiagnostic(Diagnostic.Create(Rules.SpellBuff, one.Location, one.Message));
                    }
                }

                // A base class's constructor sets it on each listed class below it too: the first of them by name is named.
                foreach (var one in set)
                {
                    var below = listed.Keys.Where(type => Below(type, one.Holder)).OrderBy(type => type.Name, StringComparer.Ordinal).FirstOrDefault();
                    if (below != null)
                    {
                        var where = SymbolEqualityComparer.Default.Equals(below, one.Holder) ? string.Empty : ", which " + one.Holder.Name + " sets for it";
                        end.ReportDiagnostic(Diagnostic.Create(Rules.SpellBuff, one.Location,
                            "'" + below.Name + "' is a buff a spell lists, so the game decides its " + one.Property + where + ": the spell's duration, a second cast and death end it; a Tick of its own still works"));
                    }
                }
            });
        });
    }

    /// <summary>A buff list's Set, Add or Replace in an ability class names the classes it lists; a unit's Apply of a buff class is gathered.</summary>
    private static void Called(OperationAnalysisContext context, ConcurrentDictionary<INamedTypeSymbol, bool> listed, ConcurrentBag<(INamedTypeSymbol, Location, string)> found)
    {
        var call = (IInvocationOperation)context.Operation;
        var method = call.TargetMethod;
        if (method.Name is "Set" or "Add" or "Replace" && method.ContainingType is { Name: "ObjectList" } list && IsMoonwellObjects(list.ContainingAssembly))
        {
            foreach (var argument in method.TypeArguments.OfType<INamedTypeSymbol>())
            {
                if (IsBuff(argument) && argument.Locations.Any(location => location.IsInSource))
                {
                    listed[argument] = true;
                }
            }

            return;
        }

        if (method.Name != "Apply" || !IsMoonwellUnit(method.ContainingType))
        {
            return;
        }

        foreach (var argument in call.Arguments)
        {
            var value = Unconverted(argument.Value);
            if (argument.Parameter?.Name == "buff" && value.Type is INamedTypeSymbol type && IsBuff(type))
            {
                found.Add((type, value.Syntax.GetLocation(),
                    "'" + type.Name + "' is a buff a spell lists: the game puts it on and Moonwell watches for it, so it cannot also be applied (decision 154); give the applied one a class of its own, sharing the code through a base class"));
            }
        }
    }

    /// <summary>A buff class setting what the game decides, with =, +=, ??= or in a deconstruction, is gathered with the class whose code sets it.</summary>
    private static void Assigned(OperationAnalysisContext context, ConcurrentBag<(INamedTypeSymbol, Location, string)> set)
    {
        var assignment = (IAssignmentOperation)context.Operation;
        if (context.ContainingSymbol.ContainingType is not INamedTypeSymbol type)
        {
            return;
        }

        var deconstructed = assignment is IDeconstructionAssignmentOperation;
        foreach (var target in deconstructed ? Elements(assignment.Target) : new[] { assignment.Target })
        {
            if (target is IPropertyReferenceOperation { Property: var property }
                && GameDecides.Contains(property.Name) && property.ContainingType is { Name: "Buff" } owner && IsMoonwell(owner.ContainingAssembly))
            {
                set.Add((type, (deconstructed ? target : assignment).Syntax.GetLocation(), property.Name));
            }
        }
    }

    // The items a deconstruction assigns to, in any depth of tuples.
    private static IEnumerable<IOperation> Elements(IOperation target) =>
        target is ITupleOperation tuple ? tuple.Elements.SelectMany(Elements) : new[] { target };

    // Whether a class is another or derives from it.
    private static bool Below(INamedTypeSymbol type, INamedTypeSymbol above)
    {
        for (var at = type; at != null; at = at.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(at.OriginalDefinition, above.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A [Replaces] buff class is listed by the standard spells; a buff class with no public constructor without parameters is gathered.</summary>
    private static void Declared(SymbolAnalysisContext context, ConcurrentDictionary<INamedTypeSymbol, bool> listed, ConcurrentBag<(INamedTypeSymbol, Location, string)> found)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsBuff(type))
        {
            return;
        }

        if (type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "ReplacesAttribute"))
        {
            listed[type] = true;
        }

        if (!type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public))
        {
            found.Add((type, type.Locations[0],
                "'" + type.Name + "' is a buff a spell lists, so Moonwell makes it when the game puts its buff on: give it a public constructor with no parameters"));
        }
    }
    /// <summary>The value under the implicit conversions the compiler adds.</summary>
    private static IOperation Unconverted(IOperation value)
    {
        while (value is IConversionOperation { IsImplicit: true } conversion)
        {
            value = conversion.Operand;
        }

        return value;
    }

    private static bool IsBuff(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.Name == "Buff" && current.ContainingNamespace?.ToDisplayString() == "Moonwell" && IsMoonwell(current.ContainingAssembly))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMoonwellUnit(INamedTypeSymbol? type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.Name == "Unit" && current.ContainingNamespace?.ToDisplayString() == "Moonwell" && IsMoonwell(current.ContainingAssembly))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMoonwellObjects(IAssemblySymbol? assembly) => assembly?.Name == "Moonwell.Objects";

    private static bool IsMoonwell(IAssemblySymbol? assembly) =>
        assembly?.Name is { } name && (name == "Moonwell" || name.StartsWith("Moonwell.", StringComparison.Ordinal));
}
