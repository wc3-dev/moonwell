using System.Globalization;
using Moonwell.GameData;
using Moonwell.MapFiles;

namespace Moonwell.Build
{
    /// <summary>
    /// The dummy that casts a spell for a unit or a player (B13): one unit type Moonwell declares when the map's code casts with
    /// <c>CastOn</c> or <c>CastAt</c>, and what the runtime needs to know of each spell the code names: its order, whether it lands
    /// on its caster, and the research it requires.
    /// </summary>
    public static class DummyType
    {
        /// <summary>The dummy type's id, Moonwell's own: no class of the map and no object of the source map may have it.</summary>
        public const string Id = "hMWd";

        /// <summary>The key of the dummy type among the object ids, as a buff's carrier is keyed by its class.</summary>
        public const string ClassName = "Moonwell#dummy";

        private const string Types = "global::Moonwell.ObjectTypes";

        /// <summary>The standard Channel, whose copies cast by the base order id they set, not by its own order.</summary>
        private const string Channel = "ANcl";

        /// <summary>
        /// The dummy type as an object to write: a copy of the Footman, which is no worker (a copy of the Peasant shows the
        /// idle-worker icon), with the fields of the probe's dummy (run 3, 11-03): Locust only, nothing drawn and no shadow, a flyer
        /// that cannot move, no cast animation, no attack, mana or food, hidden on the minimap, and no sight, so a dummy left
        /// standing for a channel reveals nothing around it.
        /// </summary>
        public static DeclaredObject Object { get; } = new DeclaredObject(
            ClassName,
            "unit",
            "hfoo",
            false,
            Id,
            null,
            new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["uabi"] = new[] { "Aloc" },
                ["uaen"] = 0,
                ["ucbs"] = 0f,
                ["ucol"] = 0f,
                ["ucpt"] = 0f,
                ["ufoo"] = 0,
                ["uhom"] = true,
                ["umdl"] = ".mdl",
                ["umpm"] = 0,
                ["umvr"] = 3f,
                ["umvs"] = 0,

                // MoveType.Fly.
                ["umvt"] = 2,
                ["unam"] = "Dummy (Moonwell)",
                ["ushh"] = 0f,
                ["ushw"] = 0f,
                ["usid"] = 0,
                ["usin"] = 0,
            },
            Array.Empty<string>());

        /// <summary>
        /// The registrations of dummy casts, for ObjectRegistry; none when the map's code does not cast. First the dummy's type,
        /// then, for each ability class the registry registers (the map's own and the standard ones its code names) and each
        /// ability of the source map's object files, once each, its order, whether its targets are only its caster, and each
        /// research it requires at the level it requires.
        /// </summary>
        /// <param name="casts">Whether the map's code casts with a dummy.</param>
        /// <param name="used">The used classes.</param>
        /// <param name="declared">The declared objects.</param>
        /// <param name="ids">The object id of each declared class.</param>
        /// <param name="standardClassIds">The standard id of each standard class a declared list names.</param>
        /// <param name="standard">The standard data.</param>
        /// <param name="set">The set of tables the map's values come from; null for the base tables.</param>
        /// <param name="mapAbilities">The abilities of the source map's object files (<see cref="MapAbilities"/>); none when null.</param>
        public static IReadOnlyList<string> Lines(
            bool casts,
            IReadOnlyList<UsedClass> used,
            IReadOnlyList<DeclaredObject> declared,
            IReadOnlyDictionary<string, string> ids,
            IReadOnlyDictionary<string, string> standardClassIds,
            StandardData standard,
            string? set,
            IReadOnlyList<DeclaredObject>? mapAbilities = null)
        {
            if (!casts)
            {
                return Array.Empty<string>();
            }

            var spells = new Spells(standard, set, ids, standardClassIds);
            spells.Lines.Add($"{Types}.DummyType({FourCC(Id)});");
            var objects = declared.ToDictionary(one => one.ClassName, StringComparer.Ordinal);
            foreach (var one in used.Where(one => one.Kind == "ability").OrderBy(one => one.FullName, StringComparer.Ordinal))
            {
                objects.TryGetValue(one.FullName, out var mine);
                var root = mine?.StandardRoot ?? one.StandardId;
                if (root != null)
                {
                    spells.Add(mine == null ? root : ids.TryGetValue(mine.ClassName, out var own) ? own : throw new BuildException($"{mine.ClassName} has no object id"), mine, root);
                }
            }

            foreach (var one in mapAbilities ?? Array.Empty<DeclaredObject>())
            {
                spells.Add(one.PinnedId ?? one.StandardRoot, one, one.StandardRoot);
            }

            return spells.Lines;
        }

        /// <summary>
        /// The abilities of the source map's object files (World Editor's), main and skin, each as an object with its id (its
        /// own, or a changed standard object's), its base, and what a dummy's cast needs of its changes: the base order id of a
        /// Channel copy (<c>Ncl6</c>) and its requirements (<c>areq</c>, <c>arqa</c>), as the file's text.
        /// </summary>
        /// <param name="mapFolder">The map folder that holds the object files.</param>
        /// <exception cref="BuildException">An object file cannot be read.</exception>
        public static IReadOnlyList<DeclaredObject> MapAbilities(string mapFolder)
        {
            var roots = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var changes = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            foreach (var name in new[] { Kinds.MainName(Kind.Ability), Kinds.SkinName(Kind.Ability) })
            {
                foreach (var entry in SourceObjectFile.Read(Path.Combine(mapFolder, name), Kind.Ability).Entries)
                {
                    var id = SourceObjectFile.IdOf(entry);
                    roots[id] = entry.BaseId;
                    if (!changes.TryGetValue(id, out var changed))
                    {
                        changed = new Dictionary<string, object?>(StringComparer.Ordinal);
                        changes[id] = changed;
                    }

                    foreach (var mod in entry.Mods.Where(mod => mod.Field == "Ncl6" || mod.Field == "areq" || mod.Field == "arqa"))
                    {
                        var key = mod.Level > 0 ? mod.Field + ":" + mod.Level.ToString(CultureInfo.InvariantCulture) : mod.Field;
                        changed[key] = Convert.ToString(SourceObjectFile.ValueOf(mod), CultureInfo.InvariantCulture);
                    }
                }
            }

            return roots.Select(entry => new DeclaredObject(entry.Key, "ability", entry.Value, false, entry.Key, null, changes[entry.Key], Array.Empty<string>())).ToList();
        }

        /// <summary>The registrations of the spells, written once for each ability id.</summary>
        private sealed class Spells
        {
            private readonly StandardData standard;
            private readonly string? set;
            private readonly IReadOnlyDictionary<string, string> ids;
            private readonly IReadOnlyDictionary<string, string> standardClassIds;
            private readonly Choice? targets;
            private readonly HashSet<string> written = new HashSet<string>(StringComparer.Ordinal);

            internal Spells(StandardData standard, string? set, IReadOnlyDictionary<string, string> ids, IReadOnlyDictionary<string, string> standardClassIds)
            {
                this.standard = standard;
                this.set = set;
                this.ids = ids;
                this.standardClassIds = standardClassIds;
                targets = standard.Fields("ability").FirstOrDefault(field => field.Id == "atar")?.Choice;
            }

            internal List<string> Lines { get; } = new List<string>();

            /// <summary>Writes a spell's order and requirements, from its changes (none for a standard class) over its root's values; an id already written, or one with no order, writes nothing.</summary>
            internal void Add(string id, DeclaredObject? mine, string root)
            {
                if (!written.Add(id))
                {
                    return;
                }

                var order = root == Channel ? Text(mine, root, "Ncl6", 1) : Text(mine, root, "aord", 0);
                if (string.IsNullOrEmpty(order))
                {
                    return;
                }

                Lines.Add($"{Types}.CastOrder({FourCC(id)}, {ObjectLua.Quote(order)}, {(SelfOnly(Text(mine, root, "atar", 1)) ? "true" : "false")});");
                var required = Required(mine, root);
                var levels = Split(Text(mine, root, "arqa", 0));
                for (var i = 0; i < required.Count; i++)
                {
                    var level = i < levels.Count && int.TryParse(levels[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : 1;
                    Lines.Add($"{Types}.Requires({FourCC(id)}, {FourCC(required[i])}, {level.ToString(CultureInfo.InvariantCulture)});");
                }
            }

            // A field's text: its change when it has one (a declared choice's number as its text), else its root's.
            private string? Text(DeclaredObject? mine, string root, string field, int level)
            {
                var key = level > 0 ? field + ":" + level.ToString(CultureInfo.InvariantCulture) : field;
                if (mine != null && mine.Changed.TryGetValue(key, out var value))
                {
                    return value is int number && field == "atar" && targets != null ? targets.Text(mine.ClassName, field, number) : Convert.ToString(value, CultureInfo.InvariantCulture);
                }

                return standard.Value(root, field, level, set);
            }

            // The ids a spell requires: a declared list's classes, an object file's text, else its root's.
            private List<string> Required(DeclaredObject? mine, string root)
            {
                if (mine != null && mine.Changed.TryGetValue("areq", out var value))
                {
                    if (value is IReadOnlyList<string> names)
                    {
                        return names.Select(name => ids.TryGetValue(name, out var declaredId) ? declaredId : standardClassIds.TryGetValue(name, out var standardId) ? standardId : name).ToList();
                    }

                    return Split(value as string);
                }

                return Split(standard.Value(root, "areq", 0, set));
            }
        }

        /// <summary>
        /// Whether a map's code casts with a dummy, read from its built assembly's metadata without loading it: a member reference
        /// to <c>CastOn</c> or <c>CastAt</c> of <c>Moonwell.Unit</c> or <c>Moonwell.Player</c>. A library of the map's own counts as
        /// its code.
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        public static bool UsedBy(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(CastsIn);
        }

        private static bool CastsIn(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                var typeName = metadata.GetString(type.Name);
                if ((typeName != "Unit" && typeName != "Player") || metadata.GetString(type.Namespace) != "Moonwell")
                {
                    continue;
                }

                var name = metadata.GetString(member.Name);
                if (name == "CastOn" || name == "CastAt")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Fails the build when a class of the map, or an object of the source map, has the dummy type's id.</summary>
        /// <param name="ids">The object id of each declared class and carrier.</param>
        /// <param name="sourceMapIds">The ids the source map's object files define.</param>
        /// <exception cref="BuildException">One has the dummy type's id.</exception>
        public static void Check(IReadOnlyDictionary<string, string> ids, IReadOnlySet<string> sourceMapIds)
        {
            foreach (var entry in ids.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (entry.Value == Id)
                {
                    throw new BuildException($"{entry.Key} has the id {Id}, which Moonwell keeps for the dummy that casts spells for CastOn and CastAt; give it another id");
                }
            }

            if (sourceMapIds.Contains(Id))
            {
                throw new BuildException($"the map's object files define {Id}, which Moonwell keeps for the dummy that casts spells for CastOn and CastAt; give that object another id in World Editor");
            }
        }

        // A spell whose targets allow only its caster lands on the dummy (the design's call 15); one with no targets (Avatar, a
        // summon) is not counted, as a summon cast at a place through a dummy is no mistake.
        private static bool SelfOnly(string? targets)
        {
            var parts = Split(targets);
            return parts.Count > 0 && parts.All(part => part == "self");
        }

        private static List<string> Split(string? text)
        {
            return string.IsNullOrEmpty(text)
                ? new List<string>()
                : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static string FourCC(string id)
        {
            return $"{Types}.FourCC({ObjectLua.Quote(id)})";
        }
    }
}
