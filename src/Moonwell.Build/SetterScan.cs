using System.Reflection;
using System.Reflection.Emit;

namespace Moonwell.Build
{
    // Finds a change to a declared field from code that does not run only while a class is declared (design call 5): a call to
    // its setter, to Add or Remove of its list, to Set of its reference, or to a kind base's Set or Describe. Declared fields are
    // class defaults, set once while the class is declared: in the game the sets do nothing, so one made later would be lost
    // without a word. A lambda inside a constructor is compiled to a method whose name holds <.ctor>, and counts as the
    // constructor. A method that only constructors call, straight or through other such methods, may set them too (decision
    // 277): a setup helper that classes with different bases share. A shared field's live member (MaxLife) is this object's
    // value outside the constructor, set anywhere; but a unit's Level has no live setter (the game has none), so it is set only
    // in a declared class's constructor.
    internal static class SetterScan
    {
        private const string FieldAttributeName = "Moonwell.Objects.FieldAttribute";

        // The name a set of a unit's Level is reported with.
        private const string UnitLevel = "Level";

        internal static readonly Dictionary<short, OpCode> OpCodesByValue = ReadOpCodes();

        /// <summary>Throws for the first change to a declared field that code other than a constructor can reach, or to a unit's Level outside a declared class's constructor.</summary>
        public static void Check(Assembly map)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var methods = new List<MethodBase>();
            foreach (var type in map.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                methods.AddRange(type.GetMethods(all).Concat<MethodBase>(type.GetConstructors(all)).OrderBy(method => method.Name, StringComparer.Ordinal));
            }

            var callers = new Dictionary<int, List<MethodBase>>();
            var only = ConstructorOnly(methods, callers);
            foreach (var method in methods)
            {
                if (Declares(method))
                {
                    continue;
                }

                var found = FirstChangeCalled(method, only.Contains(method.MetadataToken));
                if (found == null)
                {
                    continue;
                }

                if (found.Value.Name == UnitLevel)
                {
                    throw new BuildException($"{Where(method)} sets {UnitLevel}; a unit's level is set only in its declared class's constructor");
                }

                var outside = method.IsVirtual ? null : Outside(method, callers, only, new HashSet<int>());
                var called = outside == null ? string.Empty
                    : outside.Value.Through == method ? $", and {Where(outside.Value.Caller)} calls it"
                    : $", and {Where(outside.Value.Caller)} calls it through {Where(outside.Value.Through)}";
                throw new BuildException($"{Where(method)} {found.Value.Verb} {found.Value.Name}{called}; declared fields are set only in the constructor, or in a method that only constructors call");
            }
        }

        private static string Where(MethodBase method)
        {
            var type = MapClasses.Describe(method.DeclaringType!);
            return method.Name == ".ctor" ? type + "'s constructor" : type + "." + method.Name;
        }

        // A declared class's constructor, or a lambda inside it: the code that runs while the class is declared.
        private static bool Declares(MethodBase method)
        {
            return (method.Name == ".ctor" || method.Name.Contains("<.ctor>", StringComparison.Ordinal)) && InDeclaredClass(method.DeclaringType!);
        }

        // The map's methods that run only while a class is declared, by token: each is called, every caller is a declared
        // class's constructor or another such method, and nothing else can reach it, which a virtual method (a reaction, an
        // interface's) and one held as a delegate can be. Fills callers with who calls each of the map's methods.
        private static HashSet<int> ConstructorOnly(List<MethodBase> methods, Dictionary<int, List<MethodBase>> callers)
        {
            var held = new HashSet<int>();
            foreach (var method in methods)
            {
                foreach (var (code, token) in Instructions(method))
                {
                    var isCall = code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj;
                    if ((!isCall && code != OpCodes.Ldftn && code != OpCodes.Ldvirtftn) || Resolve(method, token) is not { } target || target.Module != method.Module)
                    {
                        continue;
                    }

                    if (!isCall)
                    {
                        held.Add(target.MetadataToken);
                    }
                    else if (callers.TryGetValue(target.MetadataToken, out var known))
                    {
                        known.Add(method);
                    }
                    else
                    {
                        callers[target.MetadataToken] = new List<MethodBase> { method };
                    }
                }
            }

            var only = new HashSet<int>(methods.Where(method => !Declares(method) && !method.IsVirtual && !held.Contains(method.MetadataToken) && callers.ContainsKey(method.MetadataToken))
                .Select(method => method.MetadataToken));
            while (only.RemoveWhere(token => callers[token].Any(caller => !Declares(caller) && !only.Contains(caller.MetadataToken))) > 0)
            {
            }

            return only;
        }

        // The caller that keeps a method from running only while a class is declared, and the method it calls on the way:
        // followed out through callers that are kept from it by a caller of their own. Null when the method itself is the
        // reason: it is virtual, held as a delegate or never called.
        private static (MethodBase Caller, MethodBase Through)? Outside(MethodBase method, Dictionary<int, List<MethodBase>> callers, HashSet<int> only, HashSet<int> seen)
        {
            if (!seen.Add(method.MetadataToken) || !callers.TryGetValue(method.MetadataToken, out var calling))
            {
                return null;
            }

            var caller = calling.FirstOrDefault(caller => !Declares(caller) && !only.Contains(caller.MetadataToken));
            return caller == null ? null : Outside(caller, callers, only, seen) ?? (caller, method);
        }

        // A class that declares fields, or a class the compiler made inside one for its lambdas (its name starts with <). An abstract
        // or generic class under a kind base declares too: its constructor runs in each class that inherits it.
        private static bool InDeclaredClass(Type type)
        {
            for (var outer = type; outer != null; outer = outer.DeclaringType)
            {
                if (Declarations.KindBaseOf(outer) != null && Declarations.StandardIdOf(outer) == null)
                {
                    return true;
                }

                if (!outer.Name.StartsWith("<", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return false;
        }

        // Each instruction of a method's body, with its operand where that is four bytes, as the token of a method or a string is.
        private static IEnumerable<(OpCode Code, int Operand)> Instructions(MethodBase method)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            for (var position = 0; il != null && position < il.Length;)
            {
                var code = il[position++];
                var opCode = OpCodesByValue[code == 0xFE ? unchecked((short)(0xFE00 | il[position++])) : code];
                var size = OperandSize(opCode.OperandType, il, position);
                yield return (opCode, size == 4 ? BitConverter.ToInt32(il, position) : 0);
                position += size;
            }
        }

        // The first change a method makes; in a method that only constructors call (levelOnly), only a set of a unit's Level.
        private static (string Verb, string Name)? FirstChangeCalled(MethodBase method, bool levelOnly)
        {
            // The list or reference being changed was read from its property just before; Set names its field by a string before it.
            string? lastProperty = null;
            string? lastString = null;
            foreach (var (opCode, operand) in Instructions(method))
            {
                if (opCode == OpCodes.Ldstr)
                {
                    lastString = method.Module.ResolveString(operand);
                }
                else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
                {
                    var target = Resolve(method, operand);
                    if (target != null && target.Name == "set_Level" && target.DeclaringType?.FullName == "Moonwell.Unit")
                    {
                        return ("sets", UnitLevel);
                    }

                    if (target != null && !levelOnly)
                    {
                        var setter = FieldPropertyOf(target, "set_");
                        if (setter != null)
                        {
                            return ("sets", setter.Name);
                        }

                        var getter = FieldPropertyOf(target, "get_");
                        if (getter != null)
                        {
                            lastProperty = getter.Name;
                        }
                        else if (IsListOrReferenceChange(target))
                        {
                            return ("changes", lastProperty ?? "a list or reference");
                        }
                        else if (target.Name == "Set" && Declarations.KindOfBase.ContainsKey(target.DeclaringType!.FullName ?? string.Empty))
                        {
                            return ("changes", lastString ?? "a field");
                        }
                        else if (target.Name == "Describe" && Declarations.KindOfBase.ContainsKey(target.DeclaringType!.FullName ?? string.Empty))
                        {
                            return ("changes", "the tooltips");
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsListOrReferenceChange(MethodBase target)
        {
            var owner = target.DeclaringType;
            if (owner == null || !owner.IsGenericType)
            {
                return false;
            }

            var definition = owner.GetGenericTypeDefinition().FullName;
            return (definition == "Moonwell.Objects.ObjectList`1" && (target.Name == "Add" || target.Name == "Remove" || target.Name == "Set" || target.Name == "Replace" || target.Name == "Clear"))
                || (definition == "Moonwell.Objects.ObjectRef`1" && target.Name == "Set");
        }

        private static MethodBase? Resolve(MethodBase method, int token)
        {
            var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            var methodArguments = method is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericArguments() : null;
            try
            {
                return method.Module.ResolveMethod(token, typeArguments, methodArguments);
            }
            catch (ArgumentException)
            {
                // A token that names a generic argument of an open type has nothing to resolve to here.
                return null;
            }
        }

        private static PropertyInfo? FieldPropertyOf(MethodBase target, string prefix)
        {
            if (!target.IsSpecialName || !target.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            foreach (var property in target.DeclaringType!.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var accessor = prefix == "set_" ? property.SetMethod : property.GetMethod;
                if (accessor == target && property.GetCustomAttributesData().Any(data => data.AttributeType.FullName == FieldAttributeName))
                {
                    return property;
                }
            }

            return null;
        }

        internal static int OperandSize(OperandType type, byte[] il, int position)
        {
            switch (type)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + (4 * BitConverter.ToInt32(il, position));
                default:
                    return 4;
            }
        }

        private static Dictionary<short, OpCode> ReadOpCodes()
        {
            var table = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(OpCode))
                {
                    var opCode = (OpCode)field.GetValue(null)!;
                    table[opCode.Value] = opCode;
                }
            }

            return table;
        }
    }
}
