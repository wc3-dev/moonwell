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
        public static string Install => "global::Moonwell.Pathing.Install(new int[] { " + string.Join(", ", Ids.Select(id => $"{Types}.FourCC({ObjectLua.Quote(id)})")) + " });";

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
