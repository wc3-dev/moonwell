using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Moonwell.Build
{
    /// <summary>
    /// The checks on what a map declares (decision 260): a tooltip placeholder that names no object, or a level its ability
    /// doesn't have, fails the build, as the game shows 0 there; two buttons in one place of a declared unit's command card
    /// or build menu (its abilities and what it trains, researches, upgrades to, sells and builds), with one hotkey or one
    /// order, more buttons than places, and a declared object nothing uses, are warnings. <c>[AllowClash]</c> on the unit and
    /// <c>[AllowUnused]</c> on the object keep them quiet.
    /// </summary>
    public static class ObjectChecks
    {
        private const string AllowUnusedName = "Moonwell.Objects.AllowUnusedAttribute";
        private const string AllowClashName = "Moonwell.Objects.AllowClashAttribute";

        // <id,Key> with anything after a second comma (",%", ",."), as the game reads it.
        private static readonly Regex Tag = new Regex(@"<([^<>,]*),([^<>,]*)(?:,[^<>]*)?>", RegexOptions.CultureInvariant);
        private static readonly Regex LevelOfKey = new Regex(@"(\d+)$", RegexOptions.CultureInvariant);

        /// <summary>What the map's code tells the checks.</summary>
        /// <param name="Named">The declared classes another class of the map names in its code (creates, adds, lists, reads), by full name.</param>
        /// <param name="AllowUnused">The classes with <c>[AllowUnused]</c>.</param>
        /// <param name="AllowClash">The classes with <c>[AllowClash]</c>.</param>
        public sealed record MapCode(IReadOnlySet<string> Named, IReadOnlySet<string> AllowUnused, IReadOnlySet<string> AllowClash);

        /// <summary>Reads what the map's code names and which classes carry the attributes that keep the warnings quiet.</summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        /// <returns>What the code tells the checks.</returns>
        public static MapCode Read(string assemblyPath)
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

        /// <summary>Fails for a bad placeholder and writes the warnings into the log.</summary>
        /// <param name="declarations">What the map declares.</param>
        /// <param name="ids">The object id of each declared class.</param>
        /// <param name="standard">The standard data.</param>
        /// <param name="set">The tables values are read from (<see cref="GameData.ValueSet"/>): null for the base tables.</param>
        /// <param name="sourceMapIds">The ids of the objects the source map defines itself.</param>
        /// <param name="placed">The ids of the units, items, destructables and doodads placed on the map.</param>
        /// <param name="code">What the map's code names.</param>
        /// <param name="log">Where the warnings go.</param>
        /// <exception cref="BuildException">A placeholder names no object, or a level its ability doesn't have.</exception>
        public static void Run(
            DeclarationSet declarations,
            IReadOnlyDictionary<string, string> ids,
            StandardData standard,
            string? set,
            IReadOnlySet<string> sourceMapIds,
            IReadOnlySet<string> placed,
            MapCode code,
            TextWriter log)
        {
            var objects = new Objects(declarations, ids, standard, set);
            Placeholders(objects, sourceMapIds);
            foreach (var warning in CommandCards(objects, code.AllowClash).Concat(Unused(declarations, ids, placed, code)))
            {
                log.WriteLine("warning: " + warning);
            }
        }

        private static void Placeholders(Objects objects, IReadOnlySet<string> sourceMapIds)
        {
            foreach (var declared in objects.Declared)
            {
                foreach (var (key, value) in declared.Changed.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (value is not string text)
                    {
                        continue;
                    }

                    foreach (Match tag in Tag.Matches(text))
                    {
                        var id = tag.Groups[1].Value;
                        var where = $"{declared.ClassName}: {FieldOf(key)} has the placeholder {tag.Value}";
                        if (!objects.Exists(id) && !sourceMapIds.Contains(id))
                        {
                            throw new BuildException($"{where}, which names no object, and the game shows 0 there (ids are case-sensitive)");
                        }

                        var level = LevelOfKey.Match(tag.Groups[2].Value);
                        var levels = objects.LevelCount(id);
                        if (level.Success && levels > 0 && int.Parse(level.Value, CultureInfo.InvariantCulture) > levels)
                        {
                            throw new BuildException($"{where}, a level past the {Count(levels, "level")} of {id}, and the game shows 0 there");
                        }
                    }
                }
            }
        }

        // The lists whose things share a unit's command card with its abilities, and the build menu's own list.
        private static readonly string[] CardLists = { "uabi", "uhab", "utra", "ures", "uupt", "useu", "usei", "umki" };
        private static readonly string[] BuildLists = { "ubui" };

        private static IEnumerable<string> CommandCards(Objects objects, IReadOnlySet<string> allowClash)
        {
            foreach (var unit in objects.Declared.Where(one => one.Kind is "unit" or "hero" && !allowClash.Contains(one.ClassName)))
            {
                // shortcut: the game's own buttons (move, stop, attack, a queue's Cancel) are not counted, add them if a full card is missed.
                foreach (var warning in Card(objects, unit, CardLists, "command card", 12).Concat(Card(objects, unit, BuildLists, "build menu", 11)))
                {
                    yield return warning;
                }
            }
        }

        private static IEnumerable<string> Card(Objects objects, DeclaredObject unit, string[] lists, string card, int places)
        {
            // A clash among the things the game itself gave the standard unit is the game's own (a Headhunter and its Berserker), so one of the map's must be in it.
            var games = lists.SelectMany(list => objects.StandardList(unit, list)).ToHashSet(StringComparer.Ordinal);
            var buttons = lists.SelectMany(list => objects.Listed(unit, list)).Distinct()
                .Select(id => (Button: objects.Button(id), Maps: !games.Contains(id)))
                .Where(one => one.Button != null)
                .Select(one => (Button: one.Button!, Maps: one.Maps || one.Button!.Declared))
                .ToList();
            // The game's own that share a place take turns in it, so they count once.
            var count = buttons.Count(one => one.Maps) + buttons.Where(one => !one.Maps).Select(one => (one.Button.X, one.Button.Y)).Distinct().Count();
            if (count > places && buttons.Any(one => one.Maps))
            {
                var beside = places < 12 ? " beside Cancel" : string.Empty;
                yield return $"{unit.ClassName}: its {card} holds {count} buttons, and has {places} places{beside} ([AllowClash] keeps it quiet)";
            }

            foreach (var place in buttons.GroupBy(one => (one.Button.X, one.Button.Y)).Where(Clashes))
            {
                yield return $"{unit.ClassName}: {Names(place.Select(one => one.Button))} at button ({place.Key.X}, {place.Key.Y}) of its {card}, so the game moves all but the first to other places ([AllowClash] keeps it quiet)";
            }

            foreach (var hotkey in buttons.Where(one => one.Button.Hotkey.Length > 0).GroupBy(one => one.Button.Hotkey.ToUpperInvariant()).Where(Clashes))
            {
                yield return $"{unit.ClassName}: {Names(hotkey.Select(one => one.Button), "have")} the hotkey {hotkey.Key} ([AllowClash] keeps it quiet)";
            }

            foreach (var order in buttons.Where(one => one.Button.Hotkey.Length > 0 && one.Button.Order.Length > 0).GroupBy(one => one.Button.Order).Where(Clashes))
            {
                yield return $"{unit.ClassName}: {Names(order.Select(one => one.Button), "take")} the order {order.Key}, so only one of them casts ([AllowClash] keeps it quiet)";
            }
        }

        private static bool Clashes<TKey>(IGrouping<TKey, (Button Button, bool Maps)> group)
        {
            return group.Count() > 1 && group.Any(one => one.Maps);
        }

        // "A and B are both", "A, B and C all have".
        private static string Names(IEnumerable<Button> buttons, string verb = "are")
        {
            var names = buttons.Select(one => one.Name).ToList();
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1] + (names.Count == 2 ? (verb == "are" ? " are both" : " both " + verb) : (verb == "are" ? " are all" : " all " + verb));
        }

        private static IEnumerable<string> Unused(DeclarationSet declarations, IReadOnlyDictionary<string, string> ids, IReadOnlySet<string> placed, MapCode code)
        {
            var listed = new HashSet<string>(declarations.Objects.SelectMany(one => one.References), StringComparer.Ordinal);
            foreach (var declared in declarations.Objects)
            {
                if (declared.Replaces
                    || listed.Contains(declared.ClassName)
                    || code.Named.Contains(declared.ClassName)
                    || code.AllowUnused.Contains(declared.ClassName)
                    || (ids.TryGetValue(declared.ClassName, out var id) && placed.Contains(id)))
                {
                    continue;
                }

                yield return $"{declared.ClassName} is declared but nothing uses it: no other object lists it, no code names it and the map places none ([AllowUnused] keeps it quiet)";
            }
        }

        // "aub1:2" as "aub1 (level 2)".
        private static string FieldOf(string key)
        {
            var colon = key.IndexOf(':');
            return colon < 0 ? key : key.Substring(0, colon) + " (level " + key.Substring(colon + 1) + ")";
        }

        private static string Count(int count, string noun)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");
        }

        // Kept apart from Read so that nothing of the loaded assembly is on its frame when the context unloads.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static MapCode ReadIn(MapLoadContext context, string assemblyPath)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            var allowUnused = new HashSet<string>(StringComparer.Ordinal);
            var allowClash = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in context.Open(assemblyPath).GetTypes())
            {
                foreach (var attribute in type.GetCustomAttributesData())
                {
                    var name = attribute.AttributeType.FullName;
                    (name == AllowUnusedName ? allowUnused : name == AllowClashName ? allowClash : null)?.Add(MapClasses.Describe(type));
                }

                Scan(type, named);
            }

            return new MapCode(named, allowUnused, allowClash);
        }

        // The classes a type's code names, other than itself and the types inside it: what it creates, calls, reads or passes as a type argument.
        private static void Scan(Type type, HashSet<string> named)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var outer = Outermost(type);
            var typeArguments = type.IsGenericType ? type.GetGenericArguments() : null;
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il == null)
                {
                    continue;
                }

                var methodArguments = method is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericArguments() : null;
                var position = 0;
                while (position < il.Length)
                {
                    var code = il[position++];
                    var key = code == 0xFE ? unchecked((short)(0xFE00 | il[position++])) : code;
                    var opCode = SetterScan.OpCodesByValue[key];
                    if (opCode.OperandType is OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok or OperandType.InlineField)
                    {
                        Add(Resolve(method.Module, BitConverter.ToInt32(il, position), typeArguments, methodArguments), outer, named);
                    }

                    position += SetterScan.OperandSize(opCode.OperandType, il, position);
                }
            }
        }

        private static MemberInfo? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
        {
            try
            {
                return module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (Exception error) when (error is ArgumentException or TypeLoadException or FileNotFoundException or BadImageFormatException)
            {
                // A token that names a generic argument of an open type, or a type of an assembly not beside the map's, names no class of the map.
                return null;
            }
        }

        private static void Add(MemberInfo? member, Type outer, HashSet<string> named)
        {
            switch (member)
            {
                case Type type:
                    AddType(type, outer, named);
                    break;
                case MethodInfo { IsGenericMethod: true } generic:
                    AddType(generic.DeclaringType, outer, named);
                    foreach (var argument in generic.GetGenericArguments())
                    {
                        AddType(argument, outer, named);
                    }

                    break;
                case MemberInfo other:
                    AddType(other.DeclaringType, outer, named);
                    break;
            }
        }

        private static void AddType(Type? type, Type outer, HashSet<string> named)
        {
            if (type == null || type.IsGenericParameter)
            {
                return;
            }

            if (type.HasElementType)
            {
                AddType(type.GetElementType(), outer, named);
                return;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    AddType(argument, outer, named);
                }
            }

            if (Outermost(type) != outer && type.FullName != null)
            {
                named.Add(MapClasses.Describe(type.IsGenericType ? type.GetGenericTypeDefinition() : type));
            }
        }

        private static Type Outermost(Type type)
        {
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }

            return type;
        }

        /// <summary>A button on a command card: where it is, its hotkey and the order it casts with.</summary>
        private sealed record Button(string Name, bool Declared, int X, int Y, string Hotkey, string Order);

        /// <summary>The declared and standard objects, read by id.</summary>
        private sealed class Objects
        {
            private readonly DeclarationSet declarations;
            private readonly StandardData standard;
            private readonly string? set;
            private readonly Dictionary<string, DeclaredObject> byId = new Dictionary<string, DeclaredObject>(StringComparer.Ordinal);
            private readonly IReadOnlyDictionary<string, string> ids;
            private readonly IReadOnlySet<string> standardIds;
            private readonly IReadOnlySet<string> abilityIds;
            private static readonly string[] Kinds = { "ability", "item", "upgrade" };

            public Objects(DeclarationSet declarations, IReadOnlyDictionary<string, string> ids, StandardData standard, string? set)
            {
                this.declarations = declarations;
                this.ids = ids;
                this.standard = standard;
                this.set = set;
                standardIds = standard.AllIds();
                abilityIds = standard.Ids("ability");
                foreach (var declared in declarations.Objects)
                {
                    if (ids.TryGetValue(declared.ClassName, out var id))
                    {
                        byId[id] = declared;
                    }
                }
            }

            public IReadOnlyList<DeclaredObject> Declared => declarations.Objects;

            public bool Exists(string id)
            {
                return byId.ContainsKey(id) || standardIds.Contains(id);
            }

            /// <summary>An ability's number of levels; 0 for an object that is no ability, or not one of the map's or the game's.</summary>
            public int LevelCount(string id)
            {
                var declared = byId.GetValueOrDefault(id);
                var isAbility = declared != null ? declared.Kind == "ability" : abilityIds.Contains(id);
                return isAbility && int.TryParse(Value(declared, id, "alev", 0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
            }

            /// <summary>The ids of the things in a unit's list, in order.</summary>
            public IEnumerable<string> Listed(DeclaredObject unit, string field)
            {
                if (unit.Changed.TryGetValue(field, out var changed) && changed is IReadOnlyList<string> names)
                {
                    return names.Select(name => ids.TryGetValue(name, out var id) ? id : declarations.StandardClassIds.TryGetValue(name, out var standardId) ? standardId : name);
                }

                return StandardList(unit, field);
            }

            /// <summary>The ids in the list of the standard unit a declared one is based on.</summary>
            public IEnumerable<string> StandardList(DeclaredObject unit, string field)
            {
                return (standard.Value(unit.StandardRoot, field, 0, set) ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            /// <summary>A listed thing's button: null for one with no icon, or one off the card's 4 by 3 places.</summary>
            public Button? Button(string id)
            {
                var declared = byId.GetValueOrDefault(id);
                var kind = declared?.Kind ?? Kinds.FirstOrDefault(one => standard.Ids(one).Contains(id));
                var (icon, placeX, placeY, hotkey, level) = kind switch
                {
                    "ability" => ("aart", "abpx", "abpy", "ahky", 0),
                    "item" => ("iico", "ubpx", "ubpy", "uhot", 0),
                    // An upgrade's button is its next level's: the first is the one a new game shows.
                    "upgrade" => ("gar1", "gbpx", "gbpy", "ghk1", 1),
                    _ => ("uico", "ubpx", "ubpy", "uhot", 0),
                };
                if (string.IsNullOrEmpty(Value(declared, id, icon, level)))
                {
                    return null;
                }

                var x = Number(Value(declared, id, placeX, 0));
                var y = Number(Value(declared, id, placeY, 0));
                if (x is < 0 or > 3 || y is < 0 or > 2)
                {
                    return null;
                }

                // A Channel's order is its Base Order ID, so two Channels with their own differ.
                var root = declared?.StandardRoot ?? id;
                var order = kind != "ability" ? null : root == "ANcl" ? Value(declared, id, "Ncl6", 1) : Value(declared, id, "aord", 0);
                var name = declared?.ClassName ?? declarations.StandardClassIds.FirstOrDefault(pair => pair.Value == id).Key ?? id;
                return new Button(name, declared != null, x, y, Value(declared, id, hotkey, level) ?? string.Empty, order ?? string.Empty);
            }

            // What a declared object changed, else its standard root's value; for a standard object, its own.
            private string? Value(DeclaredObject? declared, string id, string field, int level)
            {
                var key = level > 0 ? field + ":" + level.ToString(CultureInfo.InvariantCulture) : field;
                if (declared != null && declared.Changed.TryGetValue(key, out var changed) && changed != null)
                {
                    return Convert.ToString(changed, CultureInfo.InvariantCulture);
                }

                return standard.Value(declared?.StandardRoot ?? id, field, level, set);
            }

            private static int Number(string? text)
            {
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
            }
        }
    }
}
