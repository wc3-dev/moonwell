using System.Globalization;
using Moonwell.GameData;
using Moonwell.MapFiles;

namespace Moonwell.Build
{
    /// <summary>
    /// The building types' placement data for <c>Paths.CanBuild</c>, <c>BuildSpots</c> and <c>BuildPlace</c> (J60): each
    /// building's cells from its pathing texture (<c>upat</c>), the ground its cells must not have (<c>upap</c>) and must have
    /// (<c>upar</c>), and whether it takes gold in or is a gold mine (game facts 11-28: a building with Return Gold and Lumber
    /// is refused within 768 of a gold mine). Written only into a map whose code calls one of them.
    /// </summary>
    public static class BuildingTypes
    {
        /// <summary>The members of <c>Moonwell.Paths</c> that read the table.</summary>
        private static readonly string[] Members = { "CanBuild", "BuildSpots", "BuildPlace" };

        // The abilities that bring gold in (Return Gold and Lumber, Return Gold) and those of a gold mine (gold mine, haunted, entangled).
        private static readonly string[] GoldReturns = { "Argl", "Argd" };
        private static readonly string[] Mines = { "Agld", "Abgm", "Aegm" };

        // The game's pathing words of upap and upar, by their bit in Paths.Buildings (Moonwell.Objects' PathingPrevent numbers).
        private static readonly (string Word, int Bit)[] Words = { ("blighted", 1), ("unbuildable", 2), ("unflyable", 4), ("unwalkable", 8), ("unamph", 16), ("unfloat", 32) };

        private const string Types = "global::Moonwell.Paths";

        /// <summary>Whether the map's code, a library of its own or a package it uses calls CanBuild, BuildSpots or BuildPlace.</summary>
        public static bool UsedBy(string assemblyPath) => PathingTypes.Calls(assemblyPath, Members);

        /// <summary>The line of the map's generated Main that gives the package the table, after Install.</summary>
        public static string Install(string table) => Types + ".Buildings(" + ObjectLua.Quote(table) + ");";

        /// <summary>
        /// Each building type as the map will have it, as <see cref="PathingTypes.Table"/> takes the unit types: the standard
        /// buildings the map's set keeps, then the map's own unit file's, then the declared classes', each replacing the one of
        /// its id before it. An entry is the type's id, its cells' text (<see cref="PathTextures.Footprint.Text"/>, <c>0x0</c> for
        /// none), the bits its cells must not have, the bits they must have, and <c>g</c> for a gold taker, <c>m</c> for a mine
        /// or <c>-</c>, the fields split by commas and the entries by semicolons. Ordered by id.
        /// </summary>
        /// <exception cref="BuildException">A building's pathing texture is neither the game's nor in the map's files.</exception>
        public static string Table(StandardData standard, GameData data, MapProject project, IReadOnlyList<DeclaredObject> declared, IReadOnlyDictionary<string, string> ids, IReadOnlyDictionary<string, string> standardClassIds)
        {
            var set = data.ValueSet;
            var abilityRoots = AbilityRoots(project, declared, ids);
            var types = new SortedDictionary<string, Building>(StringComparer.Ordinal);
            Building Standard(string id) => new Building(
                standard.Value(id, "ubdg", 0, set) == "1",
                standard.Value(id, "upat", 0, set) ?? "",
                Bits(standard.Value(id, "upap", 0, set)),
                Bits(standard.Value(id, "upar", 0, set)),
                Split(standard.Value(id, "uabi", 0, set)));
            foreach (var id in standard.Ids("unit"))
            {
                if (!standard.IsMissing(id, data.ExistenceSet))
                {
                    types[id] = Standard(id);
                }
            }

            foreach (var entry in SourceObjectFile.Read(Path.Combine(project.MapFolder, Kinds.MainName(Kind.Unit)), Kind.Unit).Entries)
            {
                var type = Standard(entry.BaseId);
                foreach (var mod in entry.Mods)
                {
                    var value = SourceObjectFile.ValueOf(mod);
                    type = mod.Field switch
                    {
                        "ubdg" => type with { IsBuilding = Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0 },
                        "upat" => type with { Texture = value as string ?? "" },
                        "upap" => type with { Avoid = Bits(value as string) },
                        "upar" => type with { Need = Bits(value as string) },
                        "uabi" => type with { Abilities = Split(value as string) },
                        _ => type,
                    };
                }

                types[SourceObjectFile.IdOf(entry)] = type;
            }

            foreach (var one in declared.Where(one => one.Kind is "unit" or "hero"))
            {
                var type = Standard(one.StandardRoot);
                foreach (var (field, value) in one.Changed)
                {
                    type = field switch
                    {
                        "ubdg" when value is bool building => type with { IsBuilding = building },
                        "upat" => type with { Texture = value as string ?? "" },
                        "upap" when value is int bits => type with { Avoid = bits },
                        "upar" when value is int bits => type with { Need = bits },
                        "uabi" when value is IReadOnlyList<string> names => type with { Abilities = names.Select(name => IdOf(name, ids, standardClassIds)).ToArray() },
                        _ => type,
                    };
                }

                types[one.Replaces ? one.StandardRoot : ids[one.ClassName]] = type;
            }

            var footprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<string>();
            foreach (var (id, type) in types)
            {
                if (!type.IsBuilding)
                {
                    continue;
                }

                if (!footprints.TryGetValue(type.Texture, out var cells))
                {
                    cells = type.Texture.Length == 0 ? "0x0" : Footprint(type.Texture, project, id).Text();
                    footprints[type.Texture] = cells;
                }

                var roots = type.Abilities.Select(ability => abilityRoots.TryGetValue(ability, out var root) ? root : ability).ToList();
                var kind = roots.Any(GoldReturns.Contains) ? 'g' : roots.Any(Mines.Contains) ? 'm' : '-';
                entries.Add(id + cells + "," + type.Avoid.ToString(CultureInfo.InvariantCulture) + "," + type.Need.ToString(CultureInfo.InvariantCulture) + "," + kind);
            }

            return string.Join(";", entries);
        }

        private sealed record Building(bool IsBuilding, string Texture, int Avoid, int Need, string[] Abilities);

        // The game's own texture, else the map's: a file at that path in its map folder or its assets.
        private static PathTextures.Footprint Footprint(string texture, MapProject project, string id)
        {
            if (PathTextures.Standard(texture) is { } standard)
            {
                return standard;
            }

            var relative = texture.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            foreach (var folder in new[] { project.MapFolder, project.AssetsFolder })
            {
                var path = Path.Combine(folder, relative);
                if (File.Exists(path))
                {
                    try
                    {
                        return PathTextures.Read(File.ReadAllBytes(path));
                    }
                    catch (InvalidDataException bad)
                    {
                        throw new BuildException($"{path}, the pathing texture of the building type {id}, can't be read: {bad.Message}");
                    }
                }
            }

            throw new BuildException($"the building type {id} has the pathing texture {texture}, which is neither one of the game's nor a file in the map's folder or assets; Paths.CanBuild needs its cells");
        }

        // The standard ability each of the map's own abilities is based on: its ability file's and its declared classes'.
        private static Dictionary<string, string> AbilityRoots(MapProject project, IReadOnlyList<DeclaredObject> declared, IReadOnlyDictionary<string, string> ids)
        {
            var roots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in SourceObjectFile.Read(Path.Combine(project.MapFolder, Kinds.MainName(Kind.Ability)), Kind.Ability).Entries)
            {
                roots[SourceObjectFile.IdOf(entry)] = entry.BaseId;
            }

            foreach (var one in declared.Where(one => one.Kind == "ability" && !one.Replaces))
            {
                roots[ids[one.ClassName]] = one.StandardRoot;
            }

            return roots;
        }

        private static string IdOf(string name, IReadOnlyDictionary<string, string> ids, IReadOnlyDictionary<string, string> standardClassIds)
        {
            return ids.TryGetValue(name, out var id) ? id : standardClassIds.TryGetValue(name, out var standard) ? standard : name;
        }

        private static int Bits(string? words)
        {
            var bits = 0;
            foreach (var word in Split(words))
            {
                foreach (var (text, bit) in Words)
                {
                    if (string.Equals(word, text, StringComparison.OrdinalIgnoreCase))
                    {
                        bits |= bit;
                    }
                }
            }

            return bits;
        }

        private static string[] Split(string? list)
        {
            return string.IsNullOrEmpty(list) ? Array.Empty<string>() : list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
