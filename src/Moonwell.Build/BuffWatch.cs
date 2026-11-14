namespace Moonwell.Build
{
    /// <summary>
    /// The buff classes the runtime watches for (decision 152): a declared buff class whose game buff an ability lists, the
    /// map's own ability or a standard one (a <c>[Replaces]</c> class of a standard buff is listed by the standard spells),
    /// and which abilities list each, with whether the ability is an aura.
    /// </summary>
    public static class BuffWatch
    {
        private const string Types = "global::Moonwell.ObjectTypes";

        /// <summary>
        /// The game's aura abilities, the standard ones whose class is named for an aura (read from Moonwell.Objects, 11-03); an
        /// ability is an aura when it is one of these or a copy of one. One left out is still found by the runtime's sweep.
        /// </summary>
        private static readonly HashSet<string> Auras = new HashSet<string>(StringComparer.Ordinal)
        {
            "AHab", "AHad", "AHas", "AHa1", "AHa2", "AHa3", "AHpa", "AOae", "AOr2", "AOac", "AEah", "AEar", "AUau", "AUav",
            "ACba", "ACac", "ACav", "SCae", "ACnr", "ADhr", "ADmr", "ACah", "ACat", "ACua", "ACvp", "Atdg", "Aasl", "Aoar",
            "AIcq", "Aadx", "Abcs", "Aba1", "Aba2", "ADCa", "ADMa", "AARq", "AARw", "AARe", "Apsq", "Asof", "Astt", "AVAq",
            "AIgx", "Aabr",
        };

        /// <summary>The registrations of the watch, for ObjectRegistry: each watched class with its game buff, then each ability that lists one.</summary>
        /// <param name="declared">The declared objects.</param>
        /// <param name="ids">The object id of each declared class.</param>
        /// <param name="standardClassIds">The standard id of each standard class a declared list names.</param>
        /// <param name="standardLists">The buffs each standard ability lists, by its id.</param>
        public static IReadOnlyList<string> Lines(
            IReadOnlyList<DeclaredObject> declared,
            IReadOnlyDictionary<string, string> ids,
            IReadOnlyDictionary<string, string> standardClassIds,
            IReadOnlyDictionary<string, IReadOnlyList<string>> standardLists)
        {
            var buffs = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var one in declared.Where(one => one.Kind == "buff"))
            {
                buffs[IdOf(one, ids)] = one.ClassName;
            }

            if (buffs.Count == 0)
            {
                return Array.Empty<string>();
            }

            var abilities = new SortedDictionary<string, (string Root, IReadOnlyList<string> Buffs)>(StringComparer.Ordinal);
            foreach (var one in declared.Where(one => one.Kind == "ability"))
            {
                var listed = ListOf(one) is { } names
                    ? names.Select(name => ids.TryGetValue(name, out var id) ? id : standardClassIds.TryGetValue(name, out var standard) ? standard : name).ToList()
                    : standardLists.TryGetValue(one.StandardRoot, out var kept) ? kept : Array.Empty<string>();
                abilities[IdOf(one, ids)] = (one.StandardRoot, listed);
            }

            foreach (var entry in standardLists)
            {
                if (!abilities.ContainsKey(entry.Key))
                {
                    abilities[entry.Key] = (entry.Key, entry.Value);
                }
            }

            var watched = buffs.Keys.Where(id => abilities.Values.Any(ability => ability.Buffs.Contains(id))).ToList();
            var lines = new List<string>();
            foreach (var id in watched)
            {
                var type = "global::" + buffs[id];
                lines.Add($"{Types}.WatchedBuff<{type}>({FourCC(id)}, () => new {type}());");
            }

            foreach (var entry in abilities)
            {
                foreach (var buff in entry.Value.Buffs.Distinct().Where(watched.Contains))
                {
                    var aura = Auras.Contains(entry.Value.Root) || Auras.Contains(entry.Key);
                    lines.Add($"{Types}.ListsBuff({FourCC(entry.Key)}, {FourCC(buff)}, {(aura ? "true" : "false")});");
                }
            }

            return lines;
        }

        /// <summary>The icon of each declared buff class, for a kit BuffRow (decision 351): its Icon (fart) as the class declares it, else its standard root's, as the game gives no buff's icon while it runs (BlzGetAbilityIcon reads BTNTemp, seen 11-14); none for a class with no icon.</summary>
        /// <param name="declared">The declared objects.</param>
        /// <param name="standard">The game's standard objects.</param>
        /// <param name="set">The data set the map is built for.</param>
        public static IReadOnlyList<string> IconLines(IReadOnlyList<DeclaredObject> declared, StandardData standard, string? set)
        {
            var lines = new List<string>();
            foreach (var one in declared.Where(one => one.Kind == "buff").OrderBy(one => one.ClassName, StringComparer.Ordinal))
            {
                var icon = one.Changed.TryGetValue("fart", out var changed) && changed != null
                    ? Convert.ToString(changed, System.Globalization.CultureInfo.InvariantCulture)
                    : standard.Value(one.StandardRoot, "fart", 0, set);
                if (!string.IsNullOrEmpty(icon))
                {
                    lines.Add($"{Types}.BuffIcon<global::{one.ClassName}>(\"{icon.Replace("\\", "\\\\").Replace("\"", "\\\"")}\");");
                }
            }

            return lines;
        }

        /// <summary>The buffs each standard ability lists, from the game's data; empty when the map declares no buff, as nothing would be watched.</summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> StandardLists(StandardData standard, string? set, IReadOnlyList<DeclaredObject> declared)
        {
            var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (!declared.Any(one => one.Kind == "buff"))
            {
                return lists;
            }

            foreach (var id in standard.Ids("ability"))
            {
                var text = standard.Value(id, "abuf", 1, set);
                if (!string.IsNullOrEmpty(text))
                {
                    lists[id] = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }

            return lists;
        }

        /// <summary>The class names a declared ability's buff list holds when the class changed it; null when it keeps its standard root's.</summary>
        private static IReadOnlyList<string>? ListOf(DeclaredObject ability)
        {
            foreach (var entry in ability.Changed)
            {
                if ((entry.Key == "abuf" || entry.Key.StartsWith("abuf:", StringComparison.Ordinal)) && entry.Value is IReadOnlyList<string> names)
                {
                    return names;
                }
            }

            return null;
        }

        /// <summary>A declared class's object id: a [Replaces] class is the standard object itself.</summary>
        private static string IdOf(DeclaredObject one, IReadOnlyDictionary<string, string> ids)
        {
            if (one.Replaces)
            {
                return one.StandardRoot;
            }

            return ids.TryGetValue(one.ClassName, out var id) ? id : throw new BuildException($"{one.ClassName} has no object id");
        }

        private static string FourCC(string id)
        {
            return $"{Types}.FourCC({ObjectLua.Quote(id)})";
        }
    }
}
