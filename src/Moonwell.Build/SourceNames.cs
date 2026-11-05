using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Moonwell.Build
{
    /// <summary>
    /// What the map's source names of <c>Moonwell.Objects</c> that its built assembly keeps no reference to, yet CSharp.lua still
    /// needs to resolve the name. The assembly keeps a reference to an enum only where the code uses it as a value: a constant
    /// (<c>(int)Targets.Air</c>), a <c>case</c> label and <c>nameof</c> are folded into numbers and text by the compiler. A
    /// <c>nameof</c> keeps no reference to the classes and members it names either.
    /// </summary>
    internal static class SourceNames
    {
        private const string ObjectsAssemblyName = "Moonwell.Objects";

        private static readonly SymbolDisplayFormat DottedName = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

        /// <summary>What the code CSharp.lua compiles names, by looking at its source with a semantic model.</summary>
        /// <param name="project">The map project, whose C# files are read (not <c>bin</c>, <c>obj</c> or <c>build</c>).</param>
        /// <param name="assemblyPath">The map's built assembly; the libraries it references are beside it.</param>
        /// <param name="candidates">The names of the enums that are stubbed when named, all in <c>Moonwell.Objects</c>.</param>
        /// <returns>The candidates the source names, and the classes and members its <c>nameof</c>s name; all empty when it names none.</returns>
        public static NamedInSource Read(MapProject project, string assemblyPath, IReadOnlySet<string> candidates)
        {
            var choices = new HashSet<string>(StringComparer.Ordinal);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            var members = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var sources = CompiledFiles.Sources(project, project.BuildFolder).ToList();
            if (sources.Count == 0)
            {
                return new NamedInSource(choices, classes, new Dictionary<string, IReadOnlySet<string>>());
            }

            var assembly = Path.GetFullPath(assemblyPath);
            var libraries = CompiledFiles.LibrariesBeside(assemblyPath).ToList();

            // The stubs serve both kinds of build, so code that only the other kind has is looked at with its symbols too.
            var symbolSets = new List<string[]> { CompiledFiles.LuaSymbols(project) };
            if (sources.Select(CompiledFiles.ReadSource).Any(text => text.Contains("DEBUG", StringComparison.Ordinal) || text.Contains("RELEASE", StringComparison.Ordinal)))
            {
                symbolSets.Add(CompiledFiles.OtherKind(symbolSets[0]));
            }

            foreach (var symbols in symbolSets)
            {
                foreach (var (tree, model) in SourceModels.Of(sources, sources, Path.GetFileNameWithoutExtension(assembly), libraries, symbols))
                {
                    var root = tree.GetRoot();
                    foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                    {
                        if (candidates.Contains(name.Identifier.ValueText)
                            && EnumOf(model.GetSymbolInfo(name).Symbol) is { } symbol
                            && candidates.Contains(symbol.Name)
                            && symbol.ContainingNamespace.ToDisplayString() == "Moonwell.Objects")
                        {
                            choices.Add(symbol.Name);
                        }
                    }

                    foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        if (call.Expression is not IdentifierNameSyntax { Identifier.ValueText: "nameof" } || model.GetOperation(call) is not INameOfOperation)
                        {
                            continue;
                        }

                        // Each part of the name: nameof(Paladin.GoldCost) needs Paladin's stub as well as GoldCost's.
                        foreach (var part in call.ArgumentList.DescendantNodes().OfType<SimpleNameSyntax>())
                        {
                            var info = model.GetSymbolInfo(part);
                            foreach (var symbol in info.Symbol is { } one ? new[] { one } : info.CandidateSymbols.ToArray())
                            {
                                Note(symbol, classes, members);
                            }
                        }
                    }
                }
            }

            return new NamedInSource(choices, classes, members.ToDictionary(entry => entry.Key, entry => (IReadOnlySet<string>)entry.Value, StringComparer.Ordinal));
        }

        // A class of Moonwell.Objects by its dotted name; a member by its name, under its class's.
        private static void Note(ISymbol symbol, HashSet<string> classes, Dictionary<string, HashSet<string>> members)
        {
            if (symbol is INamedTypeSymbol type)
            {
                if (InObjects(type))
                {
                    classes.Add(type.ToDisplayString(DottedName));
                }

                return;
            }

            if (symbol is IPropertySymbol or IFieldSymbol or IMethodSymbol or IEventSymbol && symbol.ContainingType is { } owner && InObjects(owner))
            {
                var name = owner.ToDisplayString(DottedName);
                if (!members.TryGetValue(name, out var names))
                {
                    members[name] = names = new HashSet<string>(StringComparer.Ordinal);
                }

                names.Add(symbol.Name);
            }
        }

        private static bool InObjects(INamedTypeSymbol type)
        {
            return !type.IsGenericType && type.ContainingAssembly?.Name == ObjectsAssemblyName;
        }

        // The enum a name is, is the enum of the member it names, or is the enum an alias stands for.
        private static INamedTypeSymbol? EnumOf(ISymbol? symbol)
        {
            return symbol switch
            {
                IAliasSymbol alias => EnumOf(alias.Target),
                IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } field => field.ContainingType,
                INamedTypeSymbol { TypeKind: TypeKind.Enum } type => type,
                _ => null,
            };
        }
    }

    /// <summary>What the map's source names of <c>Moonwell.Objects</c> beyond its assembly's references (<see cref="SourceNames"/>).</summary>
    /// <param name="Choices">The names of the choice enums it names.</param>
    /// <param name="Classes">The dotted full names of the classes its <c>nameof</c>s name, as <see cref="MapClasses.Describe"/> writes them.</param>
    /// <param name="Members">The names of the members its <c>nameof</c>s name, under the dotted full name of the class that declares each.</param>
    internal sealed record NamedInSource(IReadOnlySet<string> Choices, IReadOnlySet<string> Classes, IReadOnlyDictionary<string, IReadOnlySet<string>> Members);
}
