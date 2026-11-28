using System.Globalization;
using Moonwell.GameData;
using Moonwell.MapFiles;

namespace Moonwell.Build
{
    /// <summary>
    /// The hidden helper units the pathing package tries places with (decision 243): one unit type for each square of 1 to 4
    /// cells and each movement but flying (ground, float, amphibious), declared when the map uses Moonwell.Pathing. Each is a
    /// copy of the Footman with the collision size of its square (8, 16, 32, 48), its movement type, no abilities (no
    /// Locust, which would drop units from its square, probe 11-09), nothing drawn, no shadow, sight, food or attack, and
    /// hidden on the minimap.
    /// </summary>
    public static class PathingTypes
    {
        private const string Types = "global::Moonwell.ObjectTypes";

        private static readonly float[] Collisions = { 8f, 16f, 32f, 48f };

        // MoveType.Foot, MoveType.Float and MoveType.Amphibious, in the order of Movement.Ground, Float and Amphibious.
        private static readonly int[] MoveTypes = { 0, 4, 5 };

        private static readonly string[] MovementNames = { "ground", "float", "amphibious" };

        /// <summary>The helper types' ids, Moonwell's own, in the order the package indexes them: (cells - 1) * 3 + movement.</summary>
        public static IReadOnlyList<string> Ids { get; } = Enumerable.Range(0, 12).Select(i => "hMp" + "0123456789ab"[i]).ToList();

        /// <summary>The helper types as objects to write.</summary>
        public static IReadOnlyList<DeclaredObject> Objects { get; } = Enumerable.Range(0, 12).Select(Helper).ToList();

        /// <summary>The line of the map's generated Main that installs the package with the helper types.</summary>
        public static string Install => "global::Moonwell.Paths.Install(new int[] { " + string.Join(", ", Ids.Select(id => $"{Types}.FourCC({ObjectLua.Quote(id)})")) + " });";

        /// <summary>The members of <c>Moonwell.Paths</c> that read a unit type's size or movement by its id (decision 396).</summary>
        private static readonly string[] TypeMembers = { "CollisionOf", "MovementOf", "FindPathFor" };

        // The game's text of each MoveType, by its number in a declared class's changes.
        private static readonly string[] MoveTexts = { "foot", "horse", "fly", "hover", "float", "amph" };

        /// <summary>Whether the map's code, a library of its own, or a Moonwell package the map uses calls Paths.CollisionOf, MovementOf or FindPathFor, so it needs the unit types' table (J180: a package's call would otherwise throw in a map that makes none itself).</summary>
        public static bool TypesUsedBy(string assemblyPath)
        {
            return Calls(assemblyPath, TypeMembers);
        }

        /// <summary>Whether the map's code, a library of its own, or a Moonwell package the map uses calls one of these members of Moonwell.Paths.</summary>
        internal static bool Calls(string assemblyPath, IReadOnlyCollection<string> members)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
            var packages = CompiledFiles.UsedPackages(assemblyPath)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => Path.Combine(folder, name + ".dll"))
                .Where(File.Exists);
            return CompiledFiles.MapAssemblies(assemblyPath).Concat(packages).Any(assembly => Asks(assembly, members));
        }

        /// <summary>The line of the map's generated Main that gives the package the unit types' table, after Install.</summary>
        public static string TypesInstall(string table) => "global::Moonwell.Paths.UnitTypes(" + ObjectLua.Quote(table) + ");";

        /// <summary>
        /// Each unit type's collision size and movement as the map will have them (decision 396), for <c>Paths.UnitTypes</c>:
        /// the standard units the map's set of tables keeps, then the map's own unit file's (a changed standard unit or a new
        /// one, from its standard base's values), then the declared classes' (from their standard root's), each replacing an
        /// object of its id before it, as the object files are written (<see cref="ObjectFiles.Write"/>). Ordered by id.
        /// </summary>
        /// <param name="standard">The standard data.</param>
        /// <param name="data">The map's sets of tables.</param>
        /// <param name="mapFolder">The source map's folder, which holds its unit file.</param>
        /// <param name="declared">The declared objects.</param>
        /// <param name="ids">The object id of each declared class.</param>
        /// <returns>The entries, split by commas: id, movement letter, collision size.</returns>
        public static string Table(StandardData standard, GameData data, string mapFolder, IReadOnlyList<DeclaredObject> declared, IReadOnlyDictionary<string, string> ids)
        {
            var types = new SortedDictionary<string, (string Move, float Size)>(StringComparer.Ordinal);
            (string Move, float Size) Standard(string id) => (standard.Value(id, "umvt", 0, data.ValueSet) ?? "", Number(standard.Value(id, "ucol", 0, data.ValueSet)));
            foreach (var id in standard.Ids("unit"))
            {
                if (!standard.IsMissing(id, data.ExistenceSet))
                {
                    types[id] = Standard(id);
                }
            }

            foreach (var entry in SourceObjectFile.Read(Path.Combine(mapFolder, Kinds.MainName(Kind.Unit)), Kind.Unit).Entries)
            {
                var type = Standard(entry.BaseId);
                foreach (var mod in entry.Mods)
                {
                    if (mod.Field == "ucol")
                    {
                        type.Size = Convert.ToSingle(SourceObjectFile.ValueOf(mod), CultureInfo.InvariantCulture);
                    }
                    else if (mod.Field == "umvt")
                    {
                        type.Move = SourceObjectFile.ValueOf(mod) as string ?? "";
                    }
                }

                types[SourceObjectFile.IdOf(entry)] = type;
            }

            foreach (var one in declared.Where(one => one.Kind is "unit" or "hero"))
            {
                var type = Standard(one.StandardRoot);
                if (one.Changed.TryGetValue("ucol", out var size) && size != null)
                {
                    type.Size = Convert.ToSingle(size, CultureInfo.InvariantCulture);
                }

                if (one.Changed.TryGetValue("umvt", out var move) && move is int number && number >= 0 && number < MoveTexts.Length)
                {
                    type.Move = MoveTexts[number];
                }

                types[one.Replaces ? one.StandardRoot : ids[one.ClassName]] = type;
            }

            return string.Join(",", types.Select(type => type.Key + Letter(type.Value.Move) + type.Value.Size.ToString("R", CultureInfo.InvariantCulture)));
        }

        // The letter Paths.UnitTypes reads for a movement's game text: foot, horse, hover and none walk the ground.
        private static char Letter(string move) => move switch
        {
            "fly" => 'y',
            "float" => 'f',
            "amph" => 'a',
            _ => 'g',
        };

        private static float Number(string? text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0f;
        }

        private static bool Asks(string assemblyPath, IReadOnlyCollection<string> members)
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
                if (metadata.GetString(type.Name) == "Paths" && metadata.GetString(type.Namespace) == "Moonwell" && members.Contains(metadata.GetString(member.Name)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Fails the build when one of the map's classes or the map's object files has a helper's id.</summary>
        /// <exception cref="BuildException">An id is taken.</exception>
        public static void Check(IReadOnlyDictionary<string, string> ids, IReadOnlySet<string> sourceMapIds)
        {
            foreach (var id in Ids)
            {
                foreach (var entry in ids.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    if (entry.Value == id)
                    {
                        throw new BuildException($"{entry.Key} has the id {id}, which Moonwell keeps for the pathing package's helper units; give it another id");
                    }
                }

                if (sourceMapIds.Contains(id))
                {
                    throw new BuildException($"the map's object files define {id}, which Moonwell keeps for the pathing package's helper units; give that object another id in World Editor");
                }
            }
        }

        private static DeclaredObject Helper(int index)
        {
            var changes = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["uabi"] = Array.Empty<string>(),
                ["uaen"] = 0,
                ["ucol"] = Collisions[index / 3],
                ["ufoo"] = 0,
                ["uhom"] = true,
                ["umdl"] = ".mdl",
                ["umvt"] = MoveTypes[index % 3],
                ["unam"] = $"Pathing helper {index / 3 + 1} {MovementNames[index % 3]} (Moonwell)",
                ["ushh"] = 0f,
                ["ushw"] = 0f,
                ["usid"] = 0,
                ["usin"] = 0,
            };
            return new DeclaredObject("Moonwell#pathing" + index, "unit", "hfoo", false, Ids[index], null, changes, Array.Empty<string>());
        }
    }
}
