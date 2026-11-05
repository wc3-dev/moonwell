using System.Reflection;
using System.Runtime.CompilerServices;

namespace Moonwell.Build
{
    /// <summary>A saved member's type: what it is stored as, and how the code reaches it.</summary>
    /// <param name="Stored">What the save stores: int, float, bool, string, time or part.</param>
    /// <param name="List">Whether it is a list of those; <paramref name="Array"/> when an array.</param>
    /// <param name="Array">Whether the list is an array.</param>
    /// <param name="Name">The C# name, after <c>global::</c>, of the enum or the part class; null for the others.</param>
    public sealed record SavedType(string Stored, bool List, bool Array, string? Name)
    {
        /// <summary>Whether the value is an enum, stored as an int.</summary>
        public bool IsEnum => Stored == "int" && Name != null;

        /// <summary>The type as the record file names it: <c>int</c>, <c>part-list</c> and the like.</summary>
        public string RecordName => List ? Stored + "-list" : Stored;
    }

    /// <summary>A saved field or property: its C# name, its number and its type.</summary>
    public sealed record SavedMember(string Name, int Number, SavedType Type);

    /// <summary>A save class or a part class of the map's code, with its saved members by number.</summary>
    /// <param name="Name">The class's C# name, after <c>global::</c>.</param>
    /// <param name="IsPart">Whether it is a part class, reached from a save's members, rather than a save class.</param>
    /// <param name="Id">A save class's <c>[SaveId]</c>, or empty; empty for a part.</param>
    /// <param name="Members">The saved members, by number.</param>
    public sealed record SaveClass(string Name, bool IsPart, string Id, IReadOnlyList<SavedMember> Members);

    /// <summary>
    /// Reads the map's save classes and the part classes they reach from its built assembly and the libraries of its own
    /// (<see cref="CompiledFiles.MapAssemblies"/>), on .NET (decision 180).
    /// </summary>
    public static class SaveClasses
    {
        private const string SaveGameName = "Moonwell.SaveGame";

        private const string SavedName = "Moonwell.SavedAttribute";

        /// <summary>The save classes, then the part classes, each by name.</summary>
        /// <exception cref="BuildException">A saved member's type cannot be saved, which the analyzer's rule rejects first, or a library's class is not public.</exception>
        public static IReadOnlyList<SaveClass> Read(string assemblyPath)
        {
            var context = new MapLoadContext(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!);
            try
            {
                return ReadIn(context, assemblyPath);
            }
            finally
            {
                context.Unload();
            }
        }

        // Kept apart from Read so that nothing of the loaded assembly is on Read's frame when the context unloads.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IReadOnlyList<SaveClass> ReadIn(MapLoadContext context, string assemblyPath)
        {
            var main = context.Open(assemblyPath);
            var assemblies = CompiledFiles.MapAssemblies(assemblyPath).Skip(1)
                .Select(path => context.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path))))
                .Prepend(main).ToList();
            var saves = assemblies.SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsAbstract && IsSave(type)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
            var parts = new SortedDictionary<string, Type>(StringComparer.Ordinal);
            var classes = saves.Select(type => Of(type, false, IdOf(type), parts, assemblies)).ToList();

            var done = new HashSet<string>(StringComparer.Ordinal);
            while (parts.Keys.FirstOrDefault(name => !done.Contains(name)) is { } next)
            {
                done.Add(next);
                classes.Add(Of(parts[next], true, string.Empty, parts, assemblies));
            }

            // The code that writes and reads them is built into the map's own project.
            var hidden = saves.Concat(parts.Values).FirstOrDefault(type => type.Assembly != main && !type.IsVisible);
            if (hidden != null)
            {
                throw new BuildException($"{MapClasses.Describe(hidden)} in the library {hidden.Assembly.GetName().Name} is saved, so the build's code in the map's project reaches it: make it public");
            }

            return classes.Where(one => !one.IsPart).Concat(classes.Where(one => one.IsPart).OrderBy(one => one.Name, StringComparer.Ordinal)).ToList();
        }

        private static SaveClass Of(Type type, bool isPart, string id, SortedDictionary<string, Type> parts, IReadOnlyList<Assembly> map)
        {
            var members = new List<SavedMember>();
            const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;
            foreach (var member in type.GetFields(Public).Cast<MemberInfo>().Concat(type.GetProperties(Public)))
            {
                var saved = SavedOf(member);
                if (saved == null)
                {
                    continue;
                }

                var valueType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
                members.Add(new SavedMember(member.Name, (int)saved.ConstructorArguments[0].Value!, TypeOf(valueType, MapClasses.Describe(type) + "." + member.Name, parts, map)));
            }

            return new SaveClass(MapClasses.Describe(type), isPart, id, members.OrderBy(one => one.Number).ToList());
        }

        // An override keeps the [Saved] of the property it overrides, as an attribute is inherited; its own one comes first.
        private static CustomAttributeData? SavedOf(MemberInfo member)
        {
            while (true)
            {
                var saved = member.GetCustomAttributesData().FirstOrDefault(data => data.AttributeType.FullName == SavedName);
                if (saved != null || member is not PropertyInfo property || (property.GetMethod ?? property.SetMethod) is not { } accessor
                    || accessor.GetBaseDefinition().DeclaringType == accessor.DeclaringType)
                {
                    return saved;
                }

                const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                MemberInfo? overridden = null;
                for (var parent = property.DeclaringType!.BaseType; parent != null && overridden == null; parent = parent.BaseType)
                {
                    overridden = parent.GetProperties(Declared).FirstOrDefault(one => one.Name == property.Name);
                }

                if (overridden == null)
                {
                    return null;
                }

                member = overridden;
            }
        }

        private static SavedType TypeOf(Type type, string where, SortedDictionary<string, Type> parts, IReadOnlyList<Assembly> map)
        {
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                return ElementOf(type.GetElementType()!, where, parts, map) with { List = true, Array = true };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.List`1")
            {
                return ElementOf(type.GetGenericArguments()[0], where, parts, map) with { List = true };
            }

            return ElementOf(type, where, parts, map);
        }

        private static SavedType ElementOf(Type type, string where, SortedDictionary<string, Type> parts, IReadOnlyList<Assembly> map)
        {
            switch (type.FullName)
            {
                case "System.Int32":
                    return new SavedType("int", false, false, null);
                case "System.Single":
                    return new SavedType("float", false, false, null);
                case "System.Boolean":
                    return new SavedType("bool", false, false, null);
                case "System.String":
                    return new SavedType("string", false, false, null);
                case "Moonwell.RealTime":
                    return new SavedType("time", false, false, null);
            }

            if (type.IsEnum)
            {
                return new SavedType("int", false, false, MapClasses.Describe(type));
            }

            if (type.IsClass && !type.IsAbstract && !IsSave(type) && map.Contains(type.Assembly))
            {
                parts[MapClasses.Describe(type)] = type;
                return new SavedType("part", false, false, MapClasses.Describe(type));
            }

            throw new BuildException($"{where}: a {type.Name} cannot be saved; a saved field is an int, float, bool, string, enum, RealTime, a list or array of those, or a part class of the map's own");
        }

        private static string IdOf(Type type)
        {
            var id = type.GetCustomAttributesData().FirstOrDefault(data => data.AttributeType.FullName == "Moonwell.SaveIdAttribute");
            return id == null ? string.Empty : (string)id.ConstructorArguments[0].Value!;
        }

        private static bool IsSave(Type type)
        {
            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                if (parent.FullName == SaveGameName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
