using System.Globalization;

namespace Moonwell.Build
{
    /// <summary>
    /// The target lists of abilities, for <c>Ability.CanTarget</c>: the natives read no ability's Targets Allowed (seen
    /// 10-20), so a map whose code asks it gets each list of the abilities it can name, one line for each ability with the
    /// list of each of its levels, as the object data writes them.
    /// </summary>
    public static class AbilityTargets
    {
        private const string Types = "global::Moonwell.ObjectTypes";

        /// <summary>
        /// The registrations, for ObjectRegistry; none when the map's code does not ask <c>CanTarget</c>. One for each ability
        /// class the registry registers (the map's own and the standard ones its code names) and each ability of the source
        /// map's object files, once each: its changes (a declared class's, else World Editor's) over its root's values. Then,
        /// first in the list, one for every other standard ability of the set, grouped by list, as a unit may have any of them.
        /// </summary>
        /// <param name="asks">Whether the map's code asks <c>CanTarget</c>.</param>
        /// <param name="used">The used classes.</param>
        /// <param name="declared">The declared objects.</param>
        /// <param name="ids">The object id of each declared class.</param>
        /// <param name="standard">The standard data.</param>
        /// <param name="set">The set of tables the map's values come from; null for the base tables.</param>
        /// <param name="mapAbilities">The abilities of the source map's object files (<see cref="DummyType.MapAbilities"/>); none when null.</param>
        public static IReadOnlyList<string> Lines(
            bool asks,
            IReadOnlyList<UsedClass> used,
            IReadOnlyList<DeclaredObject> declared,
            IReadOnlyDictionary<string, string> ids,
            StandardData standard,
            string? set,
            IReadOnlyList<DeclaredObject>? mapAbilities = null)
        {
            var lines = new List<string>();
            if (!asks)
            {
                return lines;
            }

            var targets = standard.Fields("ability").FirstOrDefault(field => field.Id == "atar")?.Choice;
            var written = new HashSet<string>(StringComparer.Ordinal);
            void Add(string id, DeclaredObject? mine, string root)
            {
                if (!written.Add(id))
                {
                    return;
                }

                var count = int.TryParse(Text(mine, root, "alev", 0, standard, set, targets), NumberStyles.Integer, CultureInfo.InvariantCulture, out var levels) && levels > 0 ? levels : 1;
                var lists = new List<string>();
                for (var level = 1; level <= count; level++)
                {
                    lists.Add(ObjectLua.Quote(Text(mine, root, "atar", level, standard, set, targets) ?? ""));
                }

                lines.Add($"{Types}.TargetsAllowed({Types}.FourCC({ObjectLua.Quote(id)}), new string[] {{ {string.Join(", ", lists)} }});");
            }

            var objects = declared.ToDictionary(one => one.ClassName, StringComparer.Ordinal);
            foreach (var one in used.Where(one => one.Kind == "ability").OrderBy(one => one.FullName, StringComparer.Ordinal))
            {
                objects.TryGetValue(one.FullName, out var mine);
                var root = mine?.StandardRoot ?? one.StandardId;
                if (root != null)
                {
                    // A standard ability no class declares keeps World Editor's changes, as ObjectFiles keeps them.
                    var changes = mine ?? mapAbilities?.FirstOrDefault(ability => ability.PinnedId == root);
                    Add(mine == null ? root : ids.TryGetValue(mine.ClassName, out var own) ? own : throw new BuildException($"{mine.ClassName} has no object id"), changes, root);
                }
            }

            foreach (var one in mapAbilities ?? Array.Empty<DeclaredObject>())
            {
                Add(one.PinnedId ?? one.StandardRoot, one, one.StandardRoot);
            }

            // Every other standard ability, as a unit's LiveAbilities may hold any of them: grouped by list, as the 1557 have 180
            // lists and all but two the same at each level, which keep a line of their own: 9 to 13 KB of script, by the set.
            var groups = new SortedDictionary<string, System.Text.StringBuilder>(StringComparer.Ordinal);
            foreach (var id in standard.Ids("ability").OrderBy(id => id, StringComparer.Ordinal))
            {
                if (written.Contains(id) || id.Length != 4 || standard.IsMissing(id, set))
                {
                    continue;
                }

                var count = int.TryParse(standard.Value(id, "alev", 0, set), NumberStyles.Integer, CultureInfo.InvariantCulture, out var levels) && levels > 0 ? levels : 1;
                var first = standard.Value(id, "atar", 1, set) ?? "";
                var same = true;
                for (var level = 2; level <= count && same; level++)
                {
                    same = (standard.Value(id, "atar", level, set) ?? "") == first;
                }

                if (!same)
                {
                    Add(id, null, id);
                    continue;
                }

                if (!groups.TryGetValue(first, out var group))
                {
                    group = new System.Text.StringBuilder();
                    groups[first] = group;
                }

                group.Append(id);
            }

            if (groups.Count > 0)
            {
                var texts = groups.SelectMany(group => new[] { ObjectLua.Quote(group.Key), ObjectLua.Quote(group.Value.ToString()) });
                lines.Insert(0, $"{Types}.StandardTargets(new string[] {{ {string.Join(", ", texts)} }});");
            }

            return lines;
        }

        // A field's text: its change when it has one (a declared list's number as its words), else its root's.
        private static string? Text(DeclaredObject? mine, string root, string field, int level, StandardData standard, string? set, Choice? targets)
        {
            var key = level > 0 ? field + ":" + level.ToString(CultureInfo.InvariantCulture) : field;
            if (mine != null && mine.Changed.TryGetValue(key, out var value))
            {
                return value is int number && field == "atar" && targets != null ? targets.Text(mine.ClassName, field, number) : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return standard.Value(root, field, level, set);
        }

        /// <summary>
        /// Whether a map's code asks an ability's targets, read from its built assembly's metadata without loading it: a member
        /// reference to <c>CanTarget</c> of <c>Moonwell.Ability</c>. A library of the map's own counts as its code.
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        public static bool UsedBy(string assemblyPath)
        {
            return Declarations.ReferencesMember(assemblyPath, "Moonwell", "Ability", "CanTarget");
        }
    }
}
