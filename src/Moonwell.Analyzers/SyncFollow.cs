using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Moonwell.Analyzers;

/// <summary>
/// How the sync analyzer follows what code inside Local.For reaches: map methods and their overrides, the parts of a class
/// that run when it is made or first touched, operators, a using's Dispose, a foreach's enumerator, and the delegates
/// given to Local.For, to a parameter that ends there, or kept in a variable, field or event.
/// </summary>
public sealed partial class SyncAnalyzer
{
    /// <summary>Why a call may not run on one machine: the path from the call to what it reaches, and what that does.</summary>
    private sealed class Finding
    {
        public Finding(string path, string what, bool unseen = false, string? written = null, string? fix = null)
        {
            Path = path;
            What = what;
            Unseen = unseen;
            Written = written;
            Fix = fix;
        }

        public string Path { get; }

        public string What { get; }

        /// <summary>Whether the code reached is code the analyzer cannot follow, which is MW0039 and not MW0001.</summary>
        public bool Unseen { get; }

        /// <summary>The shared value the code changes, which is MW0048 and not MW0001; null for any other finding.</summary>
        public string? Written { get; }

        /// <summary>What to do about a shared value changed.</summary>
        public string? Fix { get; }

        /// <summary>The name the path starts with: what the reported code calls.</summary>
        public string Name => Path.Split(new[] { " > " }, 2, System.StringSplitOptions.None)[0];

        public Finding Through(string name) => new(name + " > " + Path, What, Unseen, Written, Fix);

        public string Message(string name)
        {
            var subject = Written ?? name;
            return Path == subject ? $"'{subject}' {What}" : $"'{subject}' {What} (through {Path})";
        }
    }

    /// <summary>A member an operation calls or touches: whether it is written, the type of what it is called on, and whether the call is virtual.</summary>
    private readonly struct Reach
    {
        public Reach(ISymbol member, bool write = false, ITypeSymbol? receiver = null, bool isVirtual = false)
        {
            Member = member;
            Write = write;
            Receiver = receiver;
            IsVirtual = isVirtual;
        }

        public ISymbol? Member { get; }

        public bool Write { get; }

        public ITypeSymbol? Receiver { get; }

        public bool IsVirtual { get; }
    }

    /// <summary>
    /// The nodes being followed now, each with its depth, the lowest depth a cycle was cut back to, and the lowest depth
    /// whose code told whether a variable is declared in the code being read.
    /// </summary>
    private sealed class Walk
    {
        public Walk(SyntaxNode? block)
        {
            if (block != null)
            {
                Code.Add((block, -1));
            }
        }

        public Dictionary<object, int> Depths { get; } = new(new NodeComparer());

        public int Low { get; set; } = int.MaxValue;

        public int Read { get; set; } = int.MaxValue;

        /// <summary>The code of one machine being read, with the depth of the node it is the code of: the block the walk began in (-1), then the code of each node followed.</summary>
        public List<(SyntaxNode Syntax, int Depth)> Code { get; } = new();
    }

    /// <summary>A part of a class that is followed as one: its static initializers and constructor.</summary>
    private sealed class Part
    {
        public Part(INamedTypeSymbol type)
        {
            Type = type;
        }

        public INamedTypeSymbol Type { get; }

        public override bool Equals(object? other) => other is Part part && SymbolEqualityComparer.Default.Equals(part.Type, Type);

        public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(Type);
    }

    /// <summary>The values given to a delegate variable, field, property or event, followed as one.</summary>
    private sealed class Values
    {
        public Values(ISymbol slot)
        {
            Slot = slot;
        }

        public ISymbol Slot { get; }

        public override bool Equals(object? other) => other is Values values && SymbolEqualityComparer.Default.Equals(values.Slot, Slot);

        public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(Slot) + 1;
    }

    /// <summary>Compares followed nodes: symbols as Roslyn compares them, anything else by its own equality.</summary>
    private sealed class NodeComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) =>
            x is ISymbol a && y is ISymbol b ? SymbolEqualityComparer.Default.Equals(a, b) : object.Equals(x, y);

        public int GetHashCode(object node) => node is ISymbol symbol ? SymbolEqualityComparer.Default.GetHashCode(symbol) : node.GetHashCode();
    }

    private sealed partial class Checker
    {
        private const string Unfollowed = "runs code the analyzer cannot follow";

        /// <summary>
        /// The members an operation calls or touches: the one it names, and those it calls without naming them (an
        /// operator, a user-defined conversion, a using's Dispose, a foreach's enumerator, a Deconstruct). A compound assignment or an
        /// increment of a property reads it and writes it.
        /// </summary>
        private IEnumerable<Reach> Targets(IOperation operation)
        {
            switch (operation)
            {
                case IInvocationOperation invocation:
                    yield return new Reach(invocation.TargetMethod, false, invocation.Instance?.Type, invocation.IsVirtual);
                    break;
                case IPropertyReferenceOperation property:
                    var virtualProperty = property.Instance is not null and not IInstanceReferenceOperation { Syntax: BaseExpressionSyntax };
                    yield return new Reach(property.Property, IsWritten(property), property.Instance?.Type, virtualProperty);
                    if (property.Parent is ICompoundAssignmentOperation or IIncrementOrDecrementOperation or ICoalesceAssignmentOperation)
                    {
                        yield return new Reach(property.Property, false, property.Instance?.Type, virtualProperty);
                    }

                    break;
                case IFieldReferenceOperation field:
                    yield return new Reach(field.Field, IsWritten(field));
                    break;
                case IMethodReferenceOperation reference:
                    yield return new Reach(reference.Method, false, reference.Instance?.Type, reference.IsVirtual);
                    break;
                case IObjectCreationOperation { Constructor: { } constructor }:
                    yield return new Reach(constructor);
                    break;
                case IBinaryOperation { OperatorMethod: { } binary }:
                    yield return new Reach(binary);
                    break;
                case IUnaryOperation { OperatorMethod: { } unary }:
                    yield return new Reach(unary);
                    break;
                case ICompoundAssignmentOperation { OperatorMethod: { } compound }:
                    yield return new Reach(compound);
                    break;
                case IIncrementOrDecrementOperation { OperatorMethod: { } increment }:
                    yield return new Reach(increment);
                    break;
                case IConversionOperation { OperatorMethod: { } conversion }:
                    yield return new Reach(conversion);
                    break;
                case IUsingOperation usingBlock:
                    foreach (var dispose in Disposes(usingBlock.Resources))
                    {
                        yield return dispose;
                    }

                    break;
                case IUsingDeclarationOperation usingDeclaration:
                    foreach (var dispose in Disposes(usingDeclaration.DeclarationGroup))
                    {
                        yield return dispose;
                    }

                    break;
                case IDeconstructionAssignmentOperation { Syntax: AssignmentExpressionSyntax assignment } deconstruction:
                    foreach (var deconstruct in Deconstructs(deconstruction.SemanticModel!.GetDeconstructionInfo(assignment)))
                    {
                        yield return deconstruct;
                    }

                    break;
                case IRecursivePatternOperation { DeconstructSymbol: IMethodSymbol deconstructs } pattern:
                    yield return new Reach(deconstructs, false, pattern.MatchedType, true);
                    break;
                case IForEachLoopOperation { Syntax: CommonForEachStatementSyntax syntax } loop:
                    if (syntax is ForEachVariableStatementSyntax variables)
                    {
                        foreach (var deconstruct in Deconstructs(loop.SemanticModel!.GetDeconstructionInfo(variables)))
                        {
                            yield return deconstruct;
                        }
                    }

                    var info = loop.SemanticModel!.GetForEachStatementInfo(syntax);
                    var collection = Unwrap(loop.Collection).Type;
                    var getEnumerator = info.GetEnumeratorMethod;
                    // The enumerator is the map's only when a GetEnumerator of the map's may run: a List's never runs the map's MoveNext.
                    var mapEnumerator = getEnumerator == null || IsMap(getEnumerator) || Overriders(getEnumerator, new Reach(getEnumerator, false, collection, true)).Any();
                    foreach (var method in new[] { getEnumerator, info.MoveNextMethod, info.CurrentProperty?.GetMethod, info.DisposeMethod })
                    {
                        if (method != null)
                        {
                            var isGetEnumerator = SymbolEqualityComparer.Default.Equals(method, getEnumerator);
                            yield return new Reach(method, false, isGetEnumerator ? collection : null, isGetEnumerator || mapEnumerator);
                        }
                    }

                    break;
            }
        }

        private static bool IsWritten(IOperation operation) => operation.Parent switch
        {
            IAssignmentOperation assignment => assignment.Target == operation,
            IIncrementOrDecrementOperation => true,
            IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } => true,
            ITupleOperation tuple => IsDeconstructed(tuple),
            _ => false,
        };

        // Whether a tuple is what a deconstruction assigns to, and not a value.
        private static bool IsDeconstructed(ITupleOperation tuple)
        {
            IOperation up = tuple;
            while (up.Parent is ITupleOperation)
            {
                up = up.Parent;
            }

            return up.Parent is IDeconstructionAssignmentOperation assignment && assignment.Target == up;
        }

        /// <summary>The Deconstruct methods a deconstruction calls, at any depth of tuples.</summary>
        private static IEnumerable<Reach> Deconstructs(DeconstructionInfo info)
        {
            if (info.Method != null)
            {
                yield return new Reach(info.Method, false, null, true);
            }

            foreach (var nested in info.Nested)
            {
                foreach (var reach in Deconstructs(nested))
                {
                    yield return reach;
                }
            }
        }

        /// <summary>The Dispose a using calls for each of its resources.</summary>
        private IEnumerable<Reach> Disposes(IOperation resources)
        {
            IEnumerable<ITypeSymbol?> types = resources is IVariableDeclarationGroupOperation group
                ? group.Declarations.SelectMany(d => d.Declarators).Select(d => (ITypeSymbol?)d.Symbol.Type)
                : new[] { resources.Type };
            var dispose = compilation.GetSpecialType(SpecialType.System_IDisposable).GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
            foreach (var type in types)
            {
                if (type == null || dispose == null)
                {
                    continue;
                }

                var method = type.TypeKind == TypeKind.Interface ? dispose : type.FindImplementationForInterfaceMember(dispose) as IMethodSymbol;
                if (method != null)
                {
                    yield return new Reach(method, false, type, true);
                }
            }
        }

        /// <summary>
        /// What an operation reaches that may not run on one machine: what its members do, the values of a delegate
        /// variable, field or event it runs, and a delegate it runs that the analyzer cannot follow.
        /// </summary>
        private Finding? Reaches(IOperation operation, Walk walk)
        {
            var from = EnclosingType(operation.Syntax, operation.SemanticModel!);
            foreach (var reach in Targets(operation))
            {
                if (Rejected(reach, walk, from) is { } found)
                {
                    return found;
                }
            }

            if (Slot(operation) is { } slot && FollowValues(slot, walk) is { } values)
            {
                return values.Through(Name(slot));
            }

            if (operation is IInvocationOperation { TargetMethod.MethodKind: MethodKind.DelegateInvoke, Instance: { } receiver } && !Followable(receiver))
            {
                return new Finding(Unwrap(receiver).Syntax.ToString(), Unfollowed, true);
            }

            if (operation is IArgumentOperation { Parameter: { } parameter } argument && MayRun(parameter) && !Followable(argument.Value))
            {
                return new Finding(Unwrap(argument.Value).Syntax.ToString(), Unfollowed, true);
            }

            return null;
        }

        /// <summary>
        /// Why a member may not be called on one machine; null when it may. The code that calls it is code of the class
        /// from (null for none), which is there already: touching its own members runs nothing of it for the first time.
        /// </summary>
        private Finding? Rejected(Reach reach, Walk walk, INamedTypeSymbol? from)
        {
            var member = reach.Member!;
            var write = reach.Write;
            var name = Name(member);
            if (IsNative(member))
            {
                if (member is IPropertySymbol)
                {
                    return write ? new Finding(name, "is set, which changes game state") : null;
                }

                if (Has(member, createsHandle))
                {
                    return new Finding(name, "creates a handle");
                }

                return Has(member, changesState) ? new Finding(name, "changes game state") : null;
            }

            if (member is IFieldSymbol or ILocalSymbol or IEventSymbol && !IsMap(member))
            {
                return null;
            }

            if (IsMoonwell(member))
            {
                // Making a Moonwell object changes nothing in the game until it is added or started.
                if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } || IsLocalSafe(member, write) || IsMachineLocal(member, write))
                {
                    return null;
                }

                return new Finding(name, "may change game state (it is not marked [LocalSafe])");
            }

            var methods = member switch
            {
                IMethodSymbol method => new[] { method },
                IPropertySymbol property => new[] { write ? property.SetMethod : property.GetMethod },
                _ => new IMethodSymbol?[0],
            };

            if (!IsMap(member))
            {
                // A library's virtual or interface member runs the map's overrides and implementations of it.
                foreach (var method in methods)
                {
                    foreach (var runs in method == null ? Enumerable.Empty<IMethodSymbol>() : Overriders(method, reach))
                    {
                        if (Follow(runs, walk) is { } inner)
                        {
                            return inner.Through(Name(runs)).Through(name);
                        }
                    }
                }

                return null;
            }

            // CSharp.lua runs a class's static initializers and static constructor when the class is first touched.
            if (member.ContainingType is { } type && !SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, from?.OriginalDefinition)
                && FollowStatics(type.OriginalDefinition, walk) is { } statics)
            {
                return statics.Through("static " + type.Name + "()").Through(name);
            }

            // [SyncUnchecked] silences the diagnostics in the method's own body; Local.For still follows it.
            if (member is IFieldSymbol or ILocalSymbol or IEventSymbol)
            {
                return null;
            }

            if (IsMachineLocal(member, write))
            {
                // Its body is checked as a block; an override without the attribute is not, so it is followed.
                foreach (var method in methods)
                {
                    foreach (var runs in method == null ? Enumerable.Empty<IMethodSymbol>() : Overriders(method, reach))
                    {
                        if (!Has(runs, machineLocal) && Follow(runs, walk) is { } overridden)
                        {
                            return overridden.Through(Name(runs)).Through(name);
                        }
                    }
                }

                return null;
            }

            if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
            {
                return FollowConstructor(constructor, walk)?.Through(name);
            }

            foreach (var method in methods)
            {
                if (method == null)
                {
                    continue;
                }

                if (Follow(method, walk) is { } inner)
                {
                    return inner.Through(name);
                }

                foreach (var runs in Overriders(method, reach))
                {
                    if (Follow(runs, walk) is { } overridden)
                    {
                        return overridden.Through(Name(runs)).Through(name);
                    }
                }
            }

            return null;
        }

        private bool IsMap(ISymbol member) => SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, compilation.Assembly);

        private bool IsMoonwell(ISymbol member) =>
            IsRuntime(member) || packages.Any(package => SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, package));

        /// <summary>
        /// The map's overrides and implementations of a virtual, abstract or interface method that a virtual call on a
        /// value of the receiver's type may run: those of a class the receiver's type converts to.
        /// </summary>
        private IEnumerable<IMethodSymbol> Overriders(IMethodSymbol method, Reach reach)
        {
            if (!reach.IsVirtual || !(method.IsVirtual || method.IsAbstract || method.IsOverride || method.ContainingType?.TypeKind == TypeKind.Interface))
            {
                yield break;
            }

            var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var yielded = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var next = new Queue<IMethodSymbol>();
            next.Enqueue(method.OriginalDefinition);
            while (next.Count > 0)
            {
                var at = next.Dequeue();
                if (!overriders.Value.TryGetValue(at, out var below))
                {
                    continue;
                }

                foreach (var (runs, type) in below)
                {
                    if (seen.Add(runs))
                    {
                        next.Enqueue(runs);
                    }

                    if (!yielded.Contains(runs)
                        && (reach.Receiver == null || reach.Receiver.TypeKind == TypeKind.TypeParameter
                            || compilation.ClassifyCommonConversion(type, reach.Receiver.OriginalDefinition).IsImplicit
                            || compilation.ClassifyCommonConversion(type, reach.Receiver).IsImplicit))
                    {
                        yielded.Add(runs);
                        yield return runs;
                    }
                }
            }
        }

        /// <summary>
        /// Each method or interface member of a class or interface, to the map's methods that override or implement it, each
        /// with the class it does so for: an implementation may be inherited from a base class that does not implement the
        /// interface itself.
        /// </summary>
        private Dictionary<IMethodSymbol, List<(IMethodSymbol Runs, INamedTypeSymbol In)>> FindOverriders()
        {
            var found = new Dictionary<IMethodSymbol, List<(IMethodSymbol Runs, INamedTypeSymbol In)>>(SymbolEqualityComparer.Default);
            void Add(IMethodSymbol above, IMethodSymbol below, INamedTypeSymbol type)
            {
                above = above.OriginalDefinition;
                below = below.OriginalDefinition;
                type = type.OriginalDefinition;
                if (SymbolEqualityComparer.Default.Equals(above, below))
                {
                    return;
                }

                if (!found.TryGetValue(above, out var list))
                {
                    found[above] = list = new List<(IMethodSymbol Runs, INamedTypeSymbol In)>();
                }

                if (!list.Any(e => SymbolEqualityComparer.Default.Equals(e.Runs, below) && SymbolEqualityComparer.Default.Equals(e.In, type)))
                {
                    list.Add((below, type));
                }
            }

            foreach (var type in Types(compilation.Assembly.GlobalNamespace))
            {
                foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if (method.OverriddenMethod is { } overridden)
                    {
                        Add(overridden, method, type);
                    }
                }

                if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    continue;
                }

                foreach (var face in type.AllInterfaces)
                {
                    foreach (var member in face.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation && IsMap(implementation))
                        {
                            Add(member, implementation, type);
                        }
                    }
                }
            }

            return found;
        }

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container)
        {
            foreach (var member in container.GetMembers())
            {
                if (member is INamespaceSymbol space)
                {
                    foreach (var type in Types(space))
                    {
                        yield return type;
                    }
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in Types(type))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>What a class's static field and property initializers and its static constructor reach.</summary>
        private Finding? FollowStatics(INamedTypeSymbol type, Walk walk) =>
            Follow(new Part(type), Initializers(type, true).Concat(type.StaticConstructors.SelectMany(Syntax)), walk);

        /// <summary>
        /// What making an object with a constructor of the map reaches: its body, its class's field and property
        /// initializers, and a primary constructor's base call, or an implicit one's base constructor.
        /// </summary>
        private Finding? FollowConstructor(IMethodSymbol constructor, Walk walk)
        {
            constructor = constructor.OriginalDefinition;
            var type = constructor.ContainingType;
            // A primary constructor's base call is in its class's base list.
            var primary = constructor.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<TypeDeclarationSyntax>().ToList();
            var baseCall = primary.SelectMany(declaration => declaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>() ?? Enumerable.Empty<PrimaryConstructorBaseTypeSyntax>()).ToList();
            var found = Follow(constructor, Syntax(constructor).Concat(baseCall).Concat(Initializers(type, false)), walk);
            if (found == null && (constructor.IsImplicitlyDeclared || (primary.Count > 0 && baseCall.Count == 0))
                && type.BaseType?.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0) is { } baseConstructor)
            {
                found = Rejected(new Reach(baseConstructor), walk, type);
            }

            return found;
        }

        /// <summary>The initializers of a class's static or instance fields and properties.</summary>
        private static IEnumerable<SyntaxNode> Initializers(INamedTypeSymbol type, bool isStatic)
        {
            foreach (var member in type.GetMembers())
            {
                if (member.IsStatic != isStatic || member.IsImplicitlyDeclared || member is not (IFieldSymbol { IsConst: false } or IPropertySymbol))
                {
                    continue;
                }

                foreach (var reference in member.DeclaringSyntaxReferences)
                {
                    var initializer = reference.GetSyntax() switch
                    {
                        VariableDeclaratorSyntax variable => variable.Initializer,
                        PropertyDeclarationSyntax property => property.Initializer,
                        _ => null,
                    };
                    if (initializer != null)
                    {
                        yield return initializer;
                    }
                }
            }
        }

        // The declarations of a method; a primary constructor's is its class's, whose parameters run nothing.
        private static IEnumerable<SyntaxNode> Syntax(IMethodSymbol method) =>
            (method.PartialImplementationPart ?? method).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).Where(syntax => syntax is not TypeDeclarationSyntax);

        /// <summary>What a method of the map reaches that may not run on one machine.</summary>
        private Finding? Follow(IMethodSymbol method, Walk walk)
        {
            method = method.OriginalDefinition;
            return method.MethodKind == MethodKind.Constructor ? FollowConstructor(method, walk) : Follow(method, Syntax(method), walk);
        }

        /// <summary>
        /// What the code of a node (a method, a part of a class, the values of a delegate) reaches that may not run on
        /// one machine. A node met again while it is still being followed is a cycle, cut there with nothing found; a
        /// result is kept for later only when no cycle was cut back to a node that is still being followed above it,
        /// since that node's own code may still find something, and when no variable it writes is declared in code
        /// around it (a local function's captured local), so the answer never depends on what was followed first.
        /// Each value of a delegate must be code the analyzer can follow.
        /// </summary>
        private Finding? Follow(object node, IEnumerable<SyntaxNode> code, Walk walk)
        {
            if (followed.TryGetValue(node, out var known))
            {
                return known;
            }

            if (walk.Depths.TryGetValue(node, out var on))
            {
                walk.Low = System.Math.Min(walk.Low, on);
                return null;
            }

            var depth = walk.Depths.Count;
            walk.Depths[node] = depth;
            var outerLow = walk.Low;
            var outerRead = walk.Read;
            walk.Low = int.MaxValue;
            walk.Read = int.MaxValue;
            Finding? found = null;
            foreach (var syntax in code)
            {
                var body = compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax);
                if (body == null)
                {
                    continue;
                }

                if (node is Values && !Followable(body))
                {
                    found = new Finding(syntax.ToString(), Unfollowed, true);
                    break;
                }

                walk.Code.Add((syntax, depth));
                foreach (var inner in body.DescendantsAndSelf())
                {
                    if ((found = Written(inner, walk) ?? Reaches(inner, walk) ?? (node is Values ? Given(inner, walk) : null)) != null)
                    {
                        break;
                    }
                }

                walk.Code.RemoveAt(walk.Code.Count - 1);
                if (found != null)
                {
                    break;
                }
            }

            walk.Depths.Remove(node);
            var low = walk.Low;
            var read = walk.Read;
            walk.Low = System.Math.Min(outerLow, low);
            walk.Read = System.Math.Min(outerRead, read);
            if (read >= depth && (found != null || low >= depth))
            {
                followed[node] = found;
            }

            return found;
        }

        // The class whose code a piece of syntax is.
        private static INamedTypeSymbol? EnclosingType(SyntaxNode syntax, SemanticModel model)
        {
            var symbol = model.GetEnclosingSymbol(syntax.SpanStart);
            return symbol as INamedTypeSymbol ?? symbol?.ContainingType;
        }

        // ---- delegates ----

        /// <summary>
        /// The delegate variable, field, property or event of the map an operation reads where the delegate may run:
        /// called, given as an argument, stored or returned. Null for any other operation; a parameter's delegates are
        /// the arguments of its callers, checked where they are written.
        /// </summary>
        private ISymbol? Slot(IOperation operation)
        {
            var slot = operation switch
            {
                ILocalReferenceOperation local => (ISymbol)local.Local,
                IFieldReferenceOperation field => field.Field,
                IPropertyReferenceOperation { Arguments.Length: 0 } property => property.Property,
                IEventReferenceOperation reference => reference.Event,
                _ => null,
            };
            if (slot == null || !IsMap(slot) || TypeOf(slot)?.TypeKind != TypeKind.Delegate)
            {
                return null;
            }

            var value = operation;
            var up = operation.Parent;
            while (up is IConversionOperation or IDelegateCreationOperation or ICoalesceOperation || up is IConditionalOperation choice && choice.Condition != value)
            {
                value = up;
                up = up.Parent;
            }

            var runs = up switch
            {
                IInvocationOperation invocation => invocation.Instance == value,
                IConditionalAccessOperation access => access.Operation == value,
                IArgumentOperation => true,
                ISimpleAssignmentOperation assignment => assignment.Value == value,
                ICompoundAssignmentOperation compound => compound.Value == value,
                IEventAssignmentOperation handler => handler.HandlerValue == value,
                IVariableInitializerOperation or IReturnOperation => true,
                _ => false,
            };
            return runs ? slot.OriginalDefinition : null;
        }

        private static ITypeSymbol? TypeOf(ISymbol symbol) => symbol switch
        {
            ILocalSymbol local => local.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IEventSymbol reference => reference.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };

        /// <summary>What the delegates given to a variable, field, property or event reach.</summary>
        private Finding? FollowValues(ISymbol slot, Walk walk) => Follow(new Values(slot), ValuesOf(slot), walk);

        /// <summary>The delegates given to a variable (in its body), or to a field, property or event (anywhere in the map): initializers and assignments.</summary>
        private IEnumerable<SyntaxNode> ValuesOf(ISymbol slot)
        {
            if (slot is IParameterSymbol parameter)
            {
                return given.GetOrAdd(parameter, Arguments) ?? Enumerable.Empty<SyntaxNode>();
            }

            return ValuesKept(slot);
        }

        private IEnumerable<SyntaxNode> ValuesKept(ISymbol slot)
        {
            if (slot is ILocalSymbol local)
            {
                foreach (var reference in local.DeclaringSyntaxReferences)
                {
                    var declarator = reference.GetSyntax();
                    if (declarator is VariableDeclaratorSyntax { Initializer.Value: { } initial })
                    {
                        yield return initial;
                    }

                    var body = BodyOf(declarator);
                    if (body == null)
                    {
                        continue;
                    }

                    var model = compilation.GetSemanticModel(declarator.SyntaxTree);
                    foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                    {
                        if (IsGiven(assignment) && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, local))
                        {
                            yield return assignment.Right;
                        }
                    }
                }

                yield break;
            }

            if (memberValues.Value.Values.TryGetValue(slot, out var values))
            {
                foreach (var value in values)
                {
                    yield return value;
                }
            }
        }

        // The body a local is declared in.
        private static SyntaxNode? BodyOf(SyntaxNode declarator) =>
            declarator.Ancestors().FirstOrDefault(a => a is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or GlobalStatementSyntax);

        /// <summary>
        /// What a delegate parameter that a kept delegate reads may run: the arguments every call of its method gives it, since
        /// the delegate kept may be any of them. A parameter that ends in Local.For, whose arguments are checked as blocks, is
        /// left out.
        /// </summary>
        private Finding? Given(IOperation operation, Walk walk)
        {
            if (operation is not IParameterReferenceOperation { Parameter: { Type.TypeKind: TypeKind.Delegate } parameter }
                || parameter.ContainingSymbol is not IMethodSymbol { MethodKind: not MethodKind.AnonymousFunction } method || !IsMap(method)
                || localParameters.Value.Contains(parameter.OriginalDefinition))
            {
                return null;
            }

            parameter = parameter.OriginalDefinition;
            if (given.GetOrAdd(parameter, Arguments) == null)
            {
                return new Finding(parameter.Name, Unfollowed, true);
            }

            return FollowValues(parameter, walk)?.Through(Name(parameter));
        }

        /// <summary>
        /// The arguments the map's calls give a parameter, a default value left out; null when they cannot all be seen: its
        /// method overrides or implements another, whose callers run it too, or is named without being called.
        /// </summary>
        private List<SyntaxNode>? Arguments(IParameterSymbol parameter)
        {
            var method = (IMethodSymbol)parameter.ContainingSymbol;
            if (method.IsOverride || Implemented(method).Any())
            {
                return null;
            }

            var constructor = method.MethodKind == MethodKind.Constructor;
            var name = constructor ? method.ContainingType.Name : method.Name;
            var found = new List<SyntaxNode>();
            foreach (var tree in compilation.SyntaxTrees)
            {
                SemanticModel? model = null;
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    var named = node switch
                    {
                        SimpleNameSyntax simple => simple.Identifier.ValueText == name,
                        ImplicitObjectCreationExpressionSyntax or ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax => constructor,
                        _ => false,
                    };
                    if (!named)
                    {
                        continue;
                    }

                    model ??= compilation.GetSemanticModel(tree);
                    var whole = node is SimpleNameSyntax ? Qualified(node) : node;
                    var call = node is SimpleNameSyntax ? whole.Parent switch
                    {
                        InvocationExpressionSyntax invocation when invocation.Expression == whole => invocation,
                        ObjectCreationExpressionSyntax creation when creation.Type == whole => creation,
                        _ => null,
                    } : node;
                    if (call == null)
                    {
                        if (model.GetOperation(whole) is IMethodReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Method.OriginalDefinition, method))
                        {
                            return null;
                        }

                        continue;
                    }

                    var arguments = model.GetOperation(call) switch
                    {
                        IInvocationOperation invocation when SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method) => invocation.Arguments,
                        IObjectCreationOperation creation when SymbolEqualityComparer.Default.Equals(creation.Constructor?.OriginalDefinition, method) => creation.Arguments,
                        _ => System.Collections.Immutable.ImmutableArray<IArgumentOperation>.Empty,
                    };
                    foreach (var argument in arguments)
                    {
                        if (argument.ArgumentKind != ArgumentKind.DefaultValue && SymbolEqualityComparer.Default.Equals(argument.Parameter?.OriginalDefinition, parameter))
                        {
                            found.Add(argument.Value.Syntax);
                        }
                    }
                }
            }

            return found;
        }

        // A name with what it is a member of: a.Name or A.B.Name.
        private static SyntaxNode Qualified(SyntaxNode name) =>
            name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access
            : name.Parent is QualifiedNameSyntax qualified && qualified.Right == name ? qualified
            : name;

        /// <summary>
        /// Whether a method may run the delegate given for a parameter: one of the map's, or of a library outside Moonwell,
        /// whose parameter is a delegate in its own declaration (not a T that is one here, as a list's Add).
        /// </summary>
        private bool MayRun(IParameterSymbol parameter) =>
            parameter.OriginalDefinition.Type.TypeKind == TypeKind.Delegate && parameter.ContainingSymbol is IMethodSymbol method
            && (IsMap(method) || !IsMoonwell(method)) && !RunsItsActionLocally(method);

        private static bool IsGiven(AssignmentExpressionSyntax assignment) =>
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression);

        /// <summary>
        /// Each delegate field, property and event of the map, to the delegates given to it: initializers and assignments;
        /// and those given a value as an out or ref argument or in a deconstruction, whose values are not all seen.
        /// </summary>
        private (Dictionary<ISymbol, List<SyntaxNode>> Values, HashSet<ISymbol> Unseen) FindMemberValues()
        {
            var found = new Dictionary<ISymbol, List<SyntaxNode>>(SymbolEqualityComparer.Default);
            var unseen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var names = new HashSet<string>();
            foreach (var type in Types(compilation.Assembly.GlobalNamespace))
            {
                foreach (var member in type.GetMembers())
                {
                    if (member.IsImplicitlyDeclared || member is not (IFieldSymbol or IPropertySymbol or IEventSymbol) || TypeOf(member)?.TypeKind != TypeKind.Delegate)
                    {
                        continue;
                    }

                    var values = found[member] = new List<SyntaxNode>();
                    names.Add(member.Name);
                    foreach (var reference in member.DeclaringSyntaxReferences)
                    {
                        var initial = reference.GetSyntax() switch
                        {
                            VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                            PropertyDeclarationSyntax property => property.Initializer?.Value,
                            _ => null,
                        };
                        if (initial != null)
                        {
                            values.Add(initial);
                        }
                    }
                }
            }

            foreach (var tree in compilation.SyntaxTrees)
            {
                SemanticModel? model = null;
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    var target = node switch
                    {
                        AssignmentExpressionSyntax assignment when IsGiven(assignment) => assignment.Left,
                        ArgumentSyntax argument when argument.RefKindKeyword.RawKind != 0 || (argument.Parent is TupleExpressionSyntax tuple && IsDeconstructed(tuple)) => argument.Expression,
                        _ => null,
                    };
                    var named = target switch
                    {
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                        _ => null,
                    };
                    if (named == null || !names.Contains(named))
                    {
                        continue;
                    }

                    model ??= compilation.GetSemanticModel(tree);
                    if (model.GetSymbolInfo(target!).Symbol?.OriginalDefinition is { } member && found.TryGetValue(member, out var values))
                    {
                        if (node is AssignmentExpressionSyntax given)
                        {
                            values.Add(given.Right);
                        }
                        else
                        {
                            unseen.Add(member);
                        }
                    }
                }
            }

            return (found, unseen);
        }

        // A delegate field, property or event of the map whose values ValuesOf reads.
        private bool ValuesSeen(ISymbol member) => IsMap(member) && !memberValues.Value.Unseen.Contains(member.OriginalDefinition);

        // The values a method's own body returns, not those of its lambdas and local functions.
        private IEnumerable<IOperation> Returned(IMethodSymbol method) =>
            Syntax(method).Select(syntax => compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax))
                .SelectMany(body => body?.DescendantsAndSelf() ?? Enumerable.Empty<IOperation>())
                .OfType<IReturnOperation>()
                .Where(returned => returned.ReturnedValue != null && !InFunction(returned))
                .Select(returned => returned.ReturnedValue!);

        private static bool InFunction(IOperation operation)
        {
            for (var up = operation.Parent; up != null; up = up.Parent)
            {
                if (up is IAnonymousFunctionOperation or ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the code a delegate value runs can be followed: a lambda, a method, a parameter of a method (whose
        /// callers' arguments are checked), a variable, field, property or event of the map whose values are all seen (and followed),
        /// a map method's result (whose body is followed, and whose returned delegates must be followable too), or null.
        /// </summary>
        private bool Followable(IOperation value, HashSet<IMethodSymbol>? seen = null)
        {
            while (true)
            {
                switch (value)
                {
                    case IConversionOperation conversion:
                        value = conversion.Operand;
                        continue;
                    case IDelegateCreationOperation creation:
                        value = creation.Target;
                        continue;
                    case IParenthesizedOperation parenthesized:
                        value = parenthesized.Operand;
                        continue;
                    case IConditionalAccessInstanceOperation instance:
                        if (Accessed(instance) is not { } accessed)
                        {
                            return false;
                        }

                        value = accessed;
                        continue;
                    case IAnonymousFunctionOperation or IMethodReferenceOperation or IDefaultValueOperation:
                        return true;
                    case ILocalReferenceOperation local:
                        return SeesEveryValue(local.Local);
                    case ILiteralOperation literal:
                        return literal.ConstantValue.HasValue && literal.ConstantValue.Value == null;
                    case IParameterReferenceOperation parameter:
                        return parameter.Parameter.ContainingSymbol is IMethodSymbol { MethodKind: not MethodKind.AnonymousFunction };
                    case IFieldReferenceOperation field:
                        return ValuesSeen(field.Field);
                    case IPropertyReferenceOperation property:
                        return ValuesSeen(property.Property);
                    case IEventReferenceOperation reference:
                        return ValuesSeen(reference.Event);
                    case IInvocationOperation invocation:
                        var method = invocation.TargetMethod.OriginalDefinition;
                        if (method.MethodKind == MethodKind.DelegateInvoke || !IsMap(method))
                        {
                            return false;
                        }

                        // shortcut: an override's returns are not checked; follow Overriders here if maps return delegates from virtual methods.
                        seen ??= new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                        return !seen.Add(method) || Returned(method).All(returned => Followable(returned, seen));
                    case IConditionalOperation choice:
                        return Followable(choice.WhenTrue, seen) && (choice.WhenFalse == null || Followable(choice.WhenFalse, seen));
                    case ICoalesceOperation coalesce:
                        return Followable(coalesce.Value, seen) && Followable(coalesce.WhenNull, seen);
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Whether every value a local gets is one ValuesOf reads: it is declared with a declarator (not by a foreach, an out
        /// var, a pattern or a deconstruction), and nothing gives it a value as an out or ref argument or in a deconstruction.
        /// </summary>
        private bool SeesEveryValue(ILocalSymbol local)
        {
            if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax declarator)
            {
                return false;
            }

            if (BodyOf(declarator) is not { } body)
            {
                return true;
            }

            var model = compilation.GetSemanticModel(declarator.SyntaxTree);
            foreach (var name in body.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (name.Identifier.ValueText == local.Name && name.Parent is ArgumentSyntax argument
                    && (argument.RefKindKeyword.RawKind != 0 || argument.Parent is TupleExpressionSyntax tuple && IsDeconstructed(tuple))
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, local))
                {
                    return false;
                }
            }

            return true;
        }

        // Whether a tuple's syntax is what a deconstruction assigns to.
        private static bool IsDeconstructed(TupleExpressionSyntax tuple)
        {
            SyntaxNode up = tuple;
            while (up.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax outer })
            {
                up = outer;
            }

            return up.Parent is AssignmentExpressionSyntax assignment && assignment.Left == up;
        }

        // The value a ?. reads: that of the access whose WhenNotNull holds the instance, as in a?.b?.c the inner access's own value holds one too.
        private static IOperation? Accessed(IConditionalAccessInstanceOperation instance)
        {
            IOperation child = instance;
            var access = instance.Parent;
            while (access != null && !(access is IConditionalAccessOperation conditional && conditional.WhenNotNull == child))
            {
                child = access;
                access = access.Parent;
            }

            return (access as IConditionalAccessOperation)?.Operation;
        }

        // ---- what code of one machine writes ----

        /// <summary>The library methods that change what they are called on, for the classes of <see cref="IsChanging"/>, and the static methods of System.Array that change an array.</summary>
        private static readonly HashSet<string> Changing = new()
        {
            "Add", "AddAfter", "AddBefore", "AddFirst", "AddLast", "AddRange", "Append", "AppendFormat", "AppendLine", "Clear", "Copy", "Dequeue",
            "Enqueue", "ExceptWith", "Fill", "Insert", "InsertRange", "IntersectWith", "Next", "NextBytes", "NextDouble", "NextInt64", "NextSingle",
            "Pop", "Push", "Remove", "RemoveAll", "RemoveAt", "RemoveFirst", "RemoveLast", "RemoveRange", "RemoveWhere", "Replace", "Reverse", "Sort",
            "SymmetricExceptWith", "TryAdd", "TryDequeue", "TryPop", "UnionWith",
        };

        // .NET's collections, and the classes of changingTypes.
        private bool IsChanging(INamedTypeSymbol? type) =>
            type != null && (changingTypes.Any(c => c != null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, c))
                || type.ContainingNamespace?.ToDisplayString() is "System.Collections.Generic" or "System.Collections");

        /// <summary>MW0048 for what code of one machine changes that every machine shares.</summary>
        public void Write(OperationAnalysisContext context)
        {
            var operation = context.Operation;
            var targets = WrittenBy(operation).ToList();
            if (targets.Count == 0 || Silenced(operation, context.ContainingSymbol) || BlockOf(operation, context.ContainingSymbol) is not { } block)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (Landed(target, new Walk(block)) is { } finding)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rules.SharedWrite, target.Syntax.GetLocation(), finding.Message(finding.Name), finding.Fix));
                }
            }
        }

        /// <summary>The first shared value an operation changes; null when it changes none.</summary>
        private Finding? Written(IOperation operation, Walk walk) =>
            WrittenBy(operation).Select(target => Landed(target, walk)).FirstOrDefault(found => found != null);

        /// <summary>What an operation writes: an assignment's or an increment's target, a ref or out argument, the event added to, and what a changing call changes.</summary>
        private IEnumerable<IOperation> WrittenBy(IOperation operation) => operation switch
        {
            IDeconstructionAssignmentOperation deconstruction => Elements(deconstruction.Target),
            IAssignmentOperation assignment => new[] { assignment.Target },
            IIncrementOrDecrementOperation increment => new[] { increment.Target },
            IEventAssignmentOperation handler => new[] { handler.EventReference },
            IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument => new[] { argument.Value },
            IInvocationOperation invocation when ChangedBy(invocation) is { } changed => new[] { changed },
            _ => Enumerable.Empty<IOperation>(),
        };

        // The items a deconstruction assigns to, in any depth of tuples.
        private static IEnumerable<IOperation> Elements(IOperation target) =>
            target is ITupleOperation tuple ? tuple.Elements.SelectMany(Elements) : new[] { target };

        /// <summary>What a call changes: its object, or the array given to System.Array's. Null for any other call.</summary>
        private IOperation? ChangedBy(IInvocationOperation invocation)
        {
            var method = invocation.TargetMethod;
            // A SortedSet's Reverse() hands its items out backwards; a List's changes the list.
            if (!Changing.Contains(method.Name) || (method.Name == "Reverse" && !method.ReturnsVoid))
            {
                return null;
            }

            if (method.IsStatic)
            {
                return IsNamed(method.ContainingType, "System.Array")
                    ? invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name is "array" or "destinationArray")?.Value
                    : null;
            }

            return IsChanging(method.ContainingType) ? invocation.Instance : null;
        }

        /// <summary>
        /// The shared value a write to a target lands in: a field, auto-property or event of the map, or a local or
        /// parameter from outside the code being read. The way goes from what is written down to what holds it. Null
        /// when the way ends in something marked [MachineLocal] or made on this machine, or names nothing.
        /// </summary>
        private Finding? Landed(IOperation target, Walk walk)
        {
            ISymbol? named = null;
            var inStruct = false;
            var passed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            for (var at = target; at != null;)
            {
                switch (at)
                {
                    case IConversionOperation conversion:
                        at = conversion.Operand;
                        break;
                    case IConditionalAccessInstanceOperation instance:
                        at = Accessed(instance);
                        break;
                    case IArrayElementReferenceOperation element:
                        at = element.ArrayReference;
                        break;
                    case IObjectCreationOperation or ITypeParameterObjectCreationOperation or IArrayCreationOperation or ICollectionExpressionOperation:
                        return null;
                    case IInstanceReferenceOperation self:
                        var made = self.ReferenceKind == InstanceReferenceKind.ImplicitReceiver
                            || ConstructorAround(self) is { } constructor && !constructor.Modifiers.Any(SyntaxKind.StaticKeyword);
                        return made ? null : Shared(named);
                    case ILocalReferenceOperation or IParameterReferenceOperation:
                        var variable = at is ILocalReferenceOperation local ? (ISymbol)local.Local : ((IParameterReferenceOperation)at).Parameter;
                        if (variable is ILocalSymbol { RefKind: not RefKind.None } alias)
                        {
                            // A ref local names what it was made from (a ref reassignment is refused), so a write through it lands there.
                            at = passed.Add(alias) ? RefMadeFrom(alias) : null;
                            break;
                        }

                        var isStruct = TypeOf(variable)?.IsValueType == true;
                        if (!IsInside(variable, walk))
                        {
                            return Shared(isStruct || named == null ? variable : named);
                        }

                        // A local that is its own value (only error CS0165) ends the way.
                        if (isStruct || !passed.Add(variable))
                        {
                            return null;
                        }

                        at = variable is ILocalSymbol held ? OnlyValue(held) : null;
                        break;
                    case IMemberReferenceOperation reference:
                        var member = reference.Member;
                        if (IsMachineLocal(member, false) || (member.IsStatic && InOwnStaticConstructor(reference)))
                        {
                            return null;
                        }

                        var holds = HoldsValue(member);
                        if (at == target && IsWritten(at) && member is IPropertySymbol && !holds && (IsMap(member) || (IsMoonwell(member) && !IsChanging(member.ContainingType))))
                        {
                            // Its setter is followed, or it is checked as a member of Moonwell.
                            return null;
                        }

                        if (holds && (named == null || inStruct))
                        {
                            named = member;
                        }

                        inStruct = member.ContainingType.IsValueType;
                        at = reference.Instance;
                        break;
                    default:
                        at = null;
                        break;
                }
            }

            return Shared(named);
        }

        private Finding? Shared(ISymbol? symbol)
        {
            if (symbol == null)
            {
                return null;
            }

            var fix = symbol switch
            {
                IEventSymbol => "an event cannot be marked: add to it outside the block",
                ILocalSymbol or IParameterSymbol => "declare it inside the block, or use a [MachineLocal] field",
                _ => "mark it [MachineLocal] if only code of one machine reads it",
            };
            return new Finding(Name(symbol), "is changed", written: Name(symbol), fix: fix);
        }

        // A field, auto-property or event of the map: what holds a value of its own, which code of one machine may not change.
        private bool HoldsValue(ISymbol member) =>
            IsMap(member) && (member is IFieldSymbol or IEventSymbol || (member is IPropertySymbol property && IsAuto(property)));

        private static bool IsAuto(IPropertySymbol property) =>
            property.ContainingType.GetMembers().OfType<IFieldSymbol>().Any(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, property));

        // Whether a static member is written in its own class's static constructor, which gives it its first value as an initializer does.
        private static bool InOwnStaticConstructor(IMemberReferenceOperation reference) =>
            ConstructorAround(reference) is { } constructor && constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
            && SymbolEqualityComparer.Default.Equals(reference.SemanticModel!.GetDeclaredSymbol(constructor)?.ContainingType, reference.Member.ContainingType);

        /// <summary>
        /// Whether a local or parameter is declared in the code being read; a setter's value is declared by no code. The
        /// walk keeps the depth of the innermost code that holds it, or -1 when none does, as the answer for a node below
        /// that depends on the code around it.
        /// </summary>
        private static bool IsInside(ISymbol variable, Walk walk)
        {
            if (variable.IsImplicitlyDeclared)
            {
                return true;
            }

            for (var i = walk.Code.Count - 1; i >= 0; i--)
            {
                var (code, depth) = walk.Code[i];
                if (variable.DeclaringSyntaxReferences.Any(declared => code.SyntaxTree == declared.SyntaxTree && code.Span.Contains(declared.Span)))
                {
                    walk.Read = System.Math.Min(walk.Read, depth);
                    return true;
                }
            }

            walk.Read = -1;
            return false;
        }

        /// <summary>What a ref local was made from: its initializer's target (ref int r = ref Score gives Score); null for one made otherwise, as by a ref foreach.</summary>
        private IOperation? RefMadeFrom(ILocalSymbol local) =>
            local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: RefExpressionSyntax made }
                ? compilation.GetSemanticModel(made.SyntaxTree).GetOperation(made.Expression)
                : null;

        /// <summary>A local's initializer, when nothing writes the local after it; null for any other local.</summary>
        private IOperation? OnlyValue(ILocalSymbol local)
        {
            if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } initial } declarator
                || BodyOf(declarator) is not { } body)
            {
                return null;
            }

            var model = compilation.GetSemanticModel(declarator.SyntaxTree);
            foreach (var node in body.DescendantNodes())
            {
                var written = node switch
                {
                    AssignmentExpressionSyntax assignment => assignment.Left,
                    ArgumentSyntax { RefKindKeyword.RawKind: not 0 } argument => argument.Expression,
                    _ => null,
                };
                if (written != null && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(written).Symbol, local))
                {
                    return null;
                }
            }

            return model.GetOperation(initial);
        }

        /// <summary>Whether an operation gives a field or auto-property a value without reading the old one: a plain =, a tuple's assignment or an out argument.</summary>
        private bool IsPlainWrite(IOperation operation, ISymbol member) =>
            (member is IFieldSymbol || (member is IPropertySymbol property && IsMap(property) && IsAuto(property))) && operation.Parent switch
            {
                ISimpleAssignmentOperation assignment => assignment.Target == operation,
                IArgumentOperation { Parameter.RefKind: RefKind.Out } => true,
                ITupleOperation tuple => IsDeconstructed(tuple),
                _ => false,
            };

        // ---- where code runs on one machine ----

        /// <summary>The outermost code around an operation that runs on one machine: a block, or a [MachineLocal] function or member. Null for code of every machine.</summary>
        private SyntaxNode? BlockOf(IOperation operation, ISymbol container)
        {
            var model = operation.SemanticModel!;
            SyntaxNode? block = null;
            for (var node = operation.Syntax; node != null; node = node.Parent)
            {
                if ((node is ExpressionSyntax expression && InLocalPosition(expression, model, localParameters.Value))
                    || (node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax && DeclaredBy(node, model) is { } function && Has(function, machineLocal)))
                {
                    block = node;
                }

                if (node is MemberDeclarationSyntax)
                {
                    return Has(container, machineLocal) ? node : block;
                }
            }

            return block;
        }

        /// <summary>Whether an operation runs on one machine: inside Local.For's action (or a delegate that ends there), or in a [MachineLocal] method.</summary>
        private bool IsLocal(IOperation operation, ISymbol container) =>
            IsLocal(operation.Syntax, operation.SemanticModel!, localParameters.Value, container);

        private bool IsLocal(SyntaxNode start, SemanticModel model, HashSet<IParameterSymbol> parameters, ISymbol? container)
        {
            for (var node = start; node != null; node = node.Parent)
            {
                if (node is ExpressionSyntax expression && InLocalPosition(expression, model, parameters))
                {
                    return true;
                }

                if (node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax
                    && DeclaredBy(node, model) is { } function && Has(function, machineLocal))
                {
                    return true;
                }

                if (node is MemberDeclarationSyntax member)
                {
                    container ??= model.GetDeclaredSymbol(member);
                    break;
                }
            }

            return Has(container, machineLocal);
        }

        /// <summary>
        /// Whether an expression is a delegate that runs on one machine: Local.For's action, or an argument for a
        /// parameter that ends there.
        /// </summary>
        private bool InLocalPosition(ExpressionSyntax expression, SemanticModel model, HashSet<IParameterSymbol> parameters)
        {
            SyntaxNode node = expression;
            while (node.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax
                   || node.Parent is PostfixUnaryExpressionSyntax postfix && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                node = node.Parent;
            }

            if (node.Parent is not ArgumentSyntax argument || argument.Parent is not BaseArgumentListSyntax list || list.Parent == null)
            {
                return false;
            }

            if (model.GetSymbolInfo(list.Parent).Symbol is not IMethodSymbol method || ParameterOf(method, argument, list) is not { } parameter)
            {
                return false;
            }

            if (method.ReducedFrom is { } extension)
            {
                // Called as an extension (x.Ext(...)): the parameters leave out this.
                method = extension;
                parameter = extension.Parameters[parameter.Ordinal + 1];
            }

            if (RunsItsActionLocally(method))
            {
                return parameter.Ordinal == 1;
            }

            return parameters.Count > 0 && parameters.Contains(parameter.OriginalDefinition);
        }

        // The parameter an argument is for: by its name, else by its place (the last one when it is params).
        private static IParameterSymbol? ParameterOf(IMethodSymbol method, ArgumentSyntax argument, BaseArgumentListSyntax list)
        {
            if (argument.NameColon != null)
            {
                return method.Parameters.FirstOrDefault(p => p.Name == argument.NameColon.Name.Identifier.ValueText);
            }

            var index = list.Arguments.IndexOf(argument);
            if (index < method.Parameters.Length)
            {
                return method.Parameters[index];
            }

            return method.Parameters.Length > 0 && method.Parameters[method.Parameters.Length - 1].IsParams ? method.Parameters[method.Parameters.Length - 1] : null;
        }

        /// <summary>
        /// The delegate parameters of the map's methods and local functions whose delegate runs on one machine: one
        /// given to Local.For or to another such parameter, or called or passed on inside Local.For. Found by repeating
        /// until nothing changes, so the order of the code does not matter.
        /// </summary>
        private HashSet<IParameterSymbol> FindLocalParameters()
        {
            var uses = new Dictionary<IParameterSymbol, List<(IdentifierNameSyntax Name, SemanticModel Model)>>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees)
            {
                SemanticModel? model = null;
                foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (syntax.Parent?.Parent is not (BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or TypeDeclarationSyntax) || syntax.Type == null)
                    {
                        continue;
                    }

                    model ??= compilation.GetSemanticModel(tree);
                    if (model.GetDeclaredSymbol(syntax) is not IParameterSymbol { Type.TypeKind: TypeKind.Delegate } parameter)
                    {
                        continue;
                    }

                    var list = uses[parameter] = new List<(IdentifierNameSyntax, SemanticModel)>();
                    foreach (var name in syntax.Parent.Parent.DescendantNodes().OfType<IdentifierNameSyntax>())
                    {
                        if (name.Identifier.ValueText == parameter.Name && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, parameter))
                        {
                            list.Add((name, model));
                        }
                    }
                }
            }

            var local = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
            bool changed;
            do
            {
                changed = false;
                foreach (var pair in uses)
                {
                    if (local.Contains(pair.Key))
                    {
                        continue;
                    }

                    foreach (var (name, model) in pair.Value)
                    {
                        if (InLocalPosition(name, model, local) || ((IsCalled(name) || name.Parent is ArgumentSyntax) && IsLocal(name, model, local, null)))
                        {
                            local.Add(pair.Key);
                            changed = true;
                            break;
                        }
                    }
                }
            }
            while (changed);

            return local;
        }

        /// <summary>Whether a name of a delegate is called: a(), a.Invoke(), a?.Invoke().</summary>
        private static bool IsCalled(ExpressionSyntax name) => name.Parent switch
        {
            InvocationExpressionSyntax invocation => invocation.Expression == name,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invoke" } access => access.Expression == name && access.Parent is InvocationExpressionSyntax,
            ConditionalAccessExpressionSyntax { WhenNotNull: InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Invoke" } } } access => access.Expression == name,
            _ => false,
        };

        /// <summary>
        /// MW0039 for a delegate given to Local.For, or to a parameter that ends there, that the analyzer cannot follow;
        /// MW0040 for such a parameter called where it runs on every machine.
        /// </summary>
        public void Delegates(OperationAnalysisContext context)
        {
            var operation = context.Operation;
            if (Silenced(operation, context.ContainingSymbol))
            {
                return;
            }

            if (operation is IArgumentOperation { Parameter: { } parameter } argument && parameter.Type.TypeKind == TypeKind.Delegate
                && (IsLocalForAction(parameter) || localParameters.Value.Contains(parameter.OriginalDefinition) || (MayRun(parameter) && IsLocal(operation, context.ContainingSymbol)))
                && !Followable(argument.Value))
            {
                var value = Unwrap(argument.Value).Syntax;
                context.ReportDiagnostic(Diagnostic.Create(Rules.Unfollowed, value.GetLocation(), new Finding(value.ToString(), Unfollowed, true).Message(value.ToString())));
            }

            if (operation is IParameterReferenceOperation reference && localParameters.Value.Contains(reference.Parameter.OriginalDefinition)
                && reference.Syntax is ExpressionSyntax name && !IsLocal(operation, context.ContainingSymbol))
            {
                var where = IsCalled(name) ? "here it runs on every machine; call it only inside Local.For"
                    : IsPassedOn(reference) ? "here it is passed on to code that may run it on every machine; pass it only to Local.For"
                    : null;
                if (where != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rules.OneMachineCode, reference.Syntax.GetLocation(), reference.Parameter.Name,
                        "is given to Local.For, so the code given for it is checked as code of one machine, and " + where));
                }
            }
        }

        /// <summary>Whether a delegate leaves for other code: given as an argument, kept in a field, property or event, or returned. A local it is kept in is left alone.</summary>
        private static bool IsPassedOn(IOperation value)
        {
            var up = value.Parent;
            while (up is IConversionOperation or IDelegateCreationOperation)
            {
                value = up;
                up = up.Parent;
            }

            return up switch
            {
                IArgumentOperation or IReturnOperation or IFieldInitializerOperation or IPropertyInitializerOperation => true,
                IAssignmentOperation assignment => assignment.Value == value && assignment.Target is not ILocalReferenceOperation,
                IEventAssignmentOperation handler => handler.HandlerValue == value,
                _ => false,
            };
        }

        /// <summary>MW0040 for [MachineLocal] on an override or an interface implementation, which is called on every machine.</summary>
        public void Placement(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            if (symbol.IsImplicitlyDeclared || machineLocal == null || Has(symbol, syncUnchecked)
                || !symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, machineLocal)))
            {
                return;
            }

            ISymbol? above = symbol switch
            {
                IMethodSymbol method => method.OverriddenMethod,
                IPropertySymbol property => property.OverriddenProperty,
                _ => null,
            };
            var how = "overrides";
            if (above == null && symbol.ContainingType != null)
            {
                above = Implemented(symbol).FirstOrDefault(i => !(IsMap(i) && Has(i, machineLocal)));
                how = "implements";
            }

            if (above != null && !(IsMap(above) && Has(above, machineLocal)) && symbol.Locations.FirstOrDefault() is { IsInSource: true } location)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.OneMachineCode, location, Name(symbol),
                    "is [MachineLocal], but it " + how + " " + Name(above) + ", which is called on every machine; put its machine-local code in Local.For(player, ...)"));
            }
        }

        // The interface members a method or property implements.
        private static IEnumerable<ISymbol> Implemented(ISymbol symbol)
        {
            var explicitly = symbol switch
            {
                IMethodSymbol method => method.ExplicitInterfaceImplementations.Cast<ISymbol>(),
                IPropertySymbol property => property.ExplicitInterfaceImplementations,
                _ => Enumerable.Empty<ISymbol>(),
            };
            foreach (var member in explicitly)
            {
                yield return member;
            }

            foreach (var face in symbol.ContainingType.AllInterfaces)
            {
                foreach (var member in face.GetMembers(symbol.Name))
                {
                    if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(member), symbol))
                    {
                        yield return member;
                    }
                }
            }
        }

        /// <summary>MW0040 for a [MachineLocal] lambda given to a method or event that runs it on every machine.</summary>
        public void LambdaPlacement(OperationAnalysisContext context)
        {
            var lambda = (IAnonymousFunctionOperation)context.Operation;
            if (!Has(lambda.Symbol, machineLocal) || Silenced(lambda, context.ContainingSymbol))
            {
                return;
            }

            IOperation value = lambda;
            var up = lambda.Parent;
            while (up is IDelegateCreationOperation or IConversionOperation)
            {
                value = up;
                up = up.Parent;
            }

            var where = up switch
            {
                IArgumentOperation { Parameter: { } parameter } when !IsLocalForAction(parameter) && !localParameters.Value.Contains(parameter.OriginalDefinition) =>
                    "is given to " + Name(parameter.ContainingSymbol),
                IEventAssignmentOperation { Adds: true, EventReference: IEventReferenceOperation reference } => "is added to " + Name(reference.Event),
                _ => null,
            };
            if (where != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.OneMachineCode, lambda.Syntax.GetLocation(), "[MachineLocal] lambda",
                    where + ", which runs it on every machine; put its machine-local code in Local.For(player, ...)"));
            }
        }

        private bool IsLocalForAction(IParameterSymbol parameter) =>
            parameter.Ordinal == 1 && parameter.ContainingSymbol is IMethodSymbol method && RunsItsActionLocally(method);

        /// <summary>Whether a method runs its second argument on one machine: Local.For, and a scope's Share.</summary>
        private bool RunsItsActionLocally(IMethodSymbol method)
        {
            var definition = method.OriginalDefinition;
            return (localFor != null && SymbolEqualityComparer.Default.Equals(definition, localFor))
                || shares.Any(share => SymbolEqualityComparer.Default.Equals(definition, share));
        }
    }
}
