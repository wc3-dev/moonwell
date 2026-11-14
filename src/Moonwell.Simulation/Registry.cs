#if !MOONWELL_LUA
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Moonwell
{
    /// <summary>
    /// A built map's register step, read from the map's assembly on .NET: the listens its code needs, then the object classes it
    /// uses with their ids and makers, as the build's ObjectRegistry gives them. A declared class has the id of its [ObjectId], or
    /// with [Replaces] its standard object's; a standard class its [Standard] id. Attributes are read by name and Moonwell.Objects
    /// is found by name, as this package references neither the build nor it. A class with no id is left out: the build refuses it.
    /// </summary>
    internal static class Registry
    {
        private const string ObjectsAssembly = "Moonwell.Objects";
        private const string StandardAttribute = "Moonwell.Objects.StandardAttribute";
        private const string ReplacesAttribute = "Moonwell.Objects.ReplacesAttribute";
        private const string ObjectIdAttribute = "Moonwell.Objects.ObjectIdAttribute";
        private const string PooledAttribute = "Moonwell.PooledAttribute";

        /// <summary>The kind bases of Moonwell.Objects by full name, and the kind of the classes under each.</summary>
        private static readonly Dictionary<string, string> KindOfBase = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Moonwell.Objects.StandardUnit"] = "unit",
            ["Moonwell.Objects.StandardHero"] = "hero",
            ["Moonwell.Objects.StandardItem"] = "item",
            ["Moonwell.Objects.StandardDestructable"] = "destructable",
            ["Moonwell.Objects.StandardDoodad"] = "doodad",
            ["Moonwell.Objects.StandardAbility"] = "ability",
            ["Moonwell.Objects.StandardBuff"] = "buff",
            ["Moonwell.Objects.StandardUpgrade"] = "upgrade",
        };

        /// <summary>Runs the map's register step: the listens, then every used class's id, then the makers, the pooled types and the buffs' displays.</summary>
        internal static void Register(Map map)
        {
            var assembly = map.GetType().Assembly;
            var objectTypes = new List<string>();
            var members = new HashSet<string>(StringComparer.Ordinal);
            ReadReferences(assembly, objectTypes, members);
            Listen(members);

            var used = Used(assembly, objectTypes);
            var replaced = new HashSet<Type>();
            foreach (var type in used)
            {
                if (IsDeclared(type) && Has(type, ReplacesAttribute) && type.BaseType != null)
                {
                    replaced.Add(type.BaseType);
                }
            }

            var registered = new List<Type>();
            var ids = new List<int>();
            foreach (var type in used)
            {
                var id = IdOf(type);
                if (id != null && id.Length == 4)
                {
                    registered.Add(type);
                    ids.Add(ObjectTypes.FourCC(id));
                }
            }

            // The ids come first, as in the build's registry: a constructor that a maker below runs may ask for any class's id.
            for (var i = 0; i < registered.Count; i++)
            {
                Call("Id", registered[i], ids[i]);
            }

            for (var i = 0; i < registered.Count; i++)
            {
                var type = registered[i];
                var kind = KindOf(type);
                var maker = MakerName(kind);
                if (maker != null && !type.IsAbstract && !replaced.Contains(type))
                {
                    Call(maker, type, ids[i], Maker(type));
                }

                if (kind == "unit" && IsDeclared(type) && Has(type, PooledAttribute))
                {
                    ObjectTypes.Pooled(ids[i]);
                }

                var carrier = kind == "buff" && IsDeclared(type) ? Named(type, ObjectIdAttribute, "Carrier") : null;
                if (carrier != null && carrier.Length == 4)
                {
                    Call("BuffDisplay", type, ids[i], ObjectTypes.FourCC(carrier));
                }
            }
        }

        /// <summary>The listens a built map's Main makes when its code reads what only they fill: last orders, selections, transports and summons.</summary>
        private static void Listen(HashSet<string> members)
        {
            if (members.Contains("Unit.get_LastOrder") || members.Contains("Unit.Reissue"))
            {
                Runtime.ListenForOrders();
            }

            if (members.Contains("Player.get_Selection"))
            {
                Runtime.ListenForSelection();
            }

            if (members.Contains("Unit.get_Transport") || members.Contains("Unit.get_Cargo"))
            {
                Runtime.ListenForTransports();
            }

            if (members.Contains("Unit.get_Summoner") || members.Contains("Unit.get_Summons"))
            {
                Runtime.ListenForSummons();
            }
        }

        /// <summary>The map's declared classes and the classes of Moonwell.Objects it names, with their bases up to the kind base, in name order.</summary>
        private static List<Type> Used(Assembly assembly, List<string> objectTypes)
        {
            var starts = new List<Type>();
            foreach (var type in TypesOf(assembly))
            {
                if (IsDeclared(type))
                {
                    starts.Add(type);
                }
            }

            var objects = Type.GetType("Moonwell.Objects.StandardUnit, " + ObjectsAssembly)?.Assembly;
            if (objects != null)
            {
                foreach (var name in objectTypes)
                {
                    var type = objects.GetType(name);
                    if (type != null && KindBaseOf(type) != null)
                    {
                        starts.Add(type);
                    }
                }
            }

            var used = new List<Type>();
            foreach (var start in starts)
            {
                for (var type = start; type != null && !KindOfBase.ContainsKey(type.FullName ?? ""); type = type.BaseType)
                {
                    if (!used.Contains(type))
                    {
                        used.Add(type);
                    }
                }
            }

            used.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return used;
        }

        /// <summary>The types of Moonwell.Objects the assembly references, by name (a nested one as Outer+Inner), and every member it references of a Moonwell type, as "Type.member".</summary>
        private static void ReadReferences(Assembly assembly, List<string> objectTypes, HashSet<string> members)
        {
            if (string.IsNullOrEmpty(assembly.Location))
            {
                return;
            }

            using var stream = File.OpenRead(assembly.Location);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeReferences)
            {
                var name = NameIn(reader, handle);
                if (name != null)
                {
                    objectTypes.Add(name);
                }
            }

            foreach (var handle in reader.MemberReferences)
            {
                var member = reader.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var parent = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (reader.GetString(parent.Namespace) == "Moonwell")
                {
                    members.Add(reader.GetString(parent.Name) + "." + reader.GetString(member.Name));
                }
            }
        }

        /// <summary>The name of a referenced type of Moonwell.Objects, a nested one through the class it is nested in; null for another assembly's.</summary>
        private static string? NameIn(MetadataReader reader, TypeReferenceHandle handle)
        {
            var reference = reader.GetTypeReference(handle);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var outer = NameIn(reader, (TypeReferenceHandle)reference.ResolutionScope);
                return outer == null ? null : outer + "+" + reader.GetString(reference.Name);
            }

            if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference)
            {
                return null;
            }

            var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope);
            if (reader.GetString(scope.Name) != ObjectsAssembly)
            {
                return null;
            }

            var space = reader.GetString(reference.Namespace);
            var name = reader.GetString(reference.Name);
            return space.Length == 0 ? name : space + "." + name;
        }

        /// <summary>The types of an assembly, those that load.</summary>
        private static Type[] TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                var loaded = new List<Type>();
                foreach (var type in error.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }

                return loaded.ToArray();
            }
        }

        /// <summary>A class of the map's own that the build constructs: not abstract or open generic, not a standard class, under a kind base.</summary>
        private static bool IsDeclared(Type type)
        {
            return !type.IsAbstract && !type.IsGenericTypeDefinition && Argument(type, StandardAttribute) == null && KindBaseOf(type) != null;
        }

        /// <summary>The kind base a class inherits; null for none.</summary>
        private static Type? KindBaseOf(Type type)
        {
            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                if (parent.FullName != null && KindOfBase.ContainsKey(parent.FullName))
                {
                    return parent;
                }
            }

            return null;
        }

        /// <summary>The kind of a class: unit, hero, item, destructable, doodad, ability, buff or upgrade.</summary>
        private static string KindOf(Type type)
        {
            return KindOfBase[KindBaseOf(type)!.FullName!];
        }

        /// <summary>A class's object id: a declared class's from [ObjectId], or its standard object's with [Replaces]; a standard class's [Standard] id; null for none.</summary>
        private static string? IdOf(Type type)
        {
            if (!IsDeclared(type))
            {
                return Argument(type, StandardAttribute);
            }

            if (!Has(type, ReplacesAttribute))
            {
                return Argument(type, ObjectIdAttribute);
            }

            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                var id = Argument(parent, StandardAttribute);
                if (id != null)
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>The ObjectTypes method that registers a kind's maker; null for a kind registered by its id alone.</summary>
        private static string? MakerName(string kind)
        {
            switch (kind)
            {
                case "unit":
                case "hero":
                    return "Unit";
                case "item":
                    return "Item";
                case "destructable":
                    return "Destructable";
                case "ability":
                    return "Ability";
                case "upgrade":
                    return "Upgrade";
                default:
                    return null;
            }
        }

        /// <summary>Calls a generic registration of ObjectTypes for a class; what it throws comes out as it is.</summary>
        private static void Call(string name, Type type, params object[] arguments)
        {
            typeof(ObjectTypes).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(type)
                .Invoke(null, BindingFlags.DoNotWrapExceptions, null, arguments, null);
        }

        /// <summary>A Func of the class that makes a new object of it, as the build's registry does with new.</summary>
        private static object Maker(Type type)
        {
            return typeof(Registry).GetMethod(nameof(MakerOf), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type).Invoke(null, null)!;
        }

        private static Func<T> MakerOf<T>()
            where T : class
        {
            return () => (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DoNotWrapExceptions, null, null, null)!;
        }

        private static bool Has(Type type, string attribute)
        {
            foreach (var data in type.GetCustomAttributesData())
            {
                if (data.AttributeType.FullName == attribute)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>An attribute's first constructor argument, as text; null without the attribute or the argument.</summary>
        private static string? Argument(Type type, string attribute)
        {
            foreach (var data in type.GetCustomAttributesData())
            {
                if (data.AttributeType.FullName == attribute)
                {
                    return data.ConstructorArguments.Count == 0 ? null : data.ConstructorArguments[0].Value as string;
                }
            }

            return null;
        }

        /// <summary>A named argument of an attribute, as text; null without it.</summary>
        private static string? Named(Type type, string attribute, string name)
        {
            foreach (var data in type.GetCustomAttributesData())
            {
                if (data.AttributeType.FullName != attribute)
                {
                    continue;
                }

                foreach (var named in data.NamedArguments)
                {
                    if (named.MemberName == name)
                    {
                        return named.TypedValue.Value as string;
                    }
                }
            }

            return null;
        }
    }
}
#endif
