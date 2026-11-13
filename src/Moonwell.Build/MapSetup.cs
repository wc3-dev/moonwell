using System.Buffers.Binary;
using System.Numerics;
using Moonwell.MapFiles;
using War3Net.Build.Common;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Info;

namespace Moonwell.Build
{
    /// <summary>
    /// Writes the map class's slots and teams into the map's info file, war3map.w3i, in place of World Editor's players and
    /// forces: the lobby reads them there, and the script's config sets them up from them (decision 198).
    /// </summary>
    public static class MapSetup
    {
        // Moonwell.Race 0 to 3 as the info file's races.
        private static readonly PlayerRace[] Races = { PlayerRace.Human, PlayerRace.Orc, PlayerRace.Undead, PlayerRace.NightElf };

        // Moonwell.Controller 0 to 3 as the info file's controllers.
        private static readonly PlayerController[] Controllers = { PlayerController.User, PlayerController.Computer, PlayerController.Rescuable, PlayerController.Neutral };

        /// <summary>Replaces the map's players and forces with the settings' slots and teams, with fixed player settings; does nothing when the map class sets up no slot.</summary>
        /// <param name="map">The opened map, with its info file and its placed units.</param>
        /// <param name="settings">The map class's settings.</param>
        /// <param name="log">The build log; a slot with no start location placed in World Editor is noted in it.</param>
        public static void Apply(War3Net.Build.Map map, MapSettings settings, TextWriter log)
        {
            var slots = settings.Slots;
            var teams = settings.Teams;
            if (slots == null || slots.Count == 0 || teams == null)
            {
                return;
            }

            var info = map.Info!;
            var starts = new Dictionary<int, Vector2>();
            foreach (var placed in map.Units?.Units ?? new List<War3Net.Build.Widget.UnitData>())
            {
                if (placed.IsPlayerStartLocation() && !starts.ContainsKey(placed.OwnerId))
                {
                    starts[placed.OwnerId] = new Vector2(placed.Position.X, placed.Position.Y);
                }
            }

            var middle = (info.CameraBounds.BottomLeft + info.CameraBounds.TopRight) / 2f;
            info.Players.Clear();
            foreach (var slot in slots.OrderBy(slot => slot.Number))
            {
                if (!starts.TryGetValue(slot.Number, out var start))
                {
                    start = middle;
                    log.WriteLine($"note: slot {slot.Number} has no start location in map\\, so it starts in the map's middle; place one for Player {slot.Number + 1} in World Editor");
                }

                info.Players.Add(new PlayerData
                {
                    Id = slot.Number,
                    Controller = Controllers[slot.Controller],
                    Race = slot.Race < 0 ? PlayerRace.Selectable : Races[slot.Race],
                    Flags = slot.Race < 0 ? PlayerFlags.FixedStartPosition | PlayerFlags.RaceSelectable : PlayerFlags.FixedStartPosition,
                    Name = slot.Name.Length > 0 ? slot.Name : "Player " + (slot.Number + 1),
                    StartPosition = start,
                    AllyLowPriorityFlags = new Bitmask32(0),
                    AllyHighPriorityFlags = new Bitmask32(0),
                    EnemyLowPriorityFlags = new Bitmask32(0),
                    EnemyHighPriorityFlags = new Bitmask32(0),
                });
            }

            info.Forces.Clear();
            for (var index = 0; index < teams.Count; index++)
            {
                var team = teams[index];
                var flags = ForceFlags.Allied;
                if (team.SharedVision)
                {
                    flags |= ForceFlags.ShareVision;
                }

                if (team.SharedControl)
                {
                    flags |= ForceFlags.ShareUnitControl;
                }

                var members = 0;
                foreach (var slot in slots.Where(slot => slot.Team == index))
                {
                    members |= 1 << slot.Number;
                }

                info.Forces.Add(new ForceData { Name = team.Name, Flags = flags, Players = new Bitmask32(members) });
            }

            info.MapFlags |= MapFlags.UseCustomForces | MapFlags.FixedPlayerSettingsForCustomForces;
        }

        /// <summary>
        /// Adds the tiles the map class declares and war3map.w3e does not list to the end of the map's tile list, in the class's
        /// order, so the game draws them; the w3e's own tiles keep their places, so no painted corner changes (decision 283).
        /// </summary>
        /// <param name="map">The opened map, with its ground.</param>
        /// <param name="settings">The map class's settings.</param>
        /// <exception cref="BuildException">A declared number is no ground tile, or the list would pass the game's limit.</exception>
        public static void ApplyTiles(War3Net.Build.Map map, MapSettings settings)
        {
            var declared = settings.Tiles;
            if (declared == null || declared.Count == 0)
            {
                return;
            }

            var ground = map.Environment ?? throw new BuildException($@"{settings.TypeName}.Tiles: the map has no map\war3map.w3e to add them to");
            var types = ground.TerrainTypes;
            var had = types.Count;
            foreach (var id in declared)
            {
                // A Tile constant is the id as the game's scripts read it, its first letter highest; the w3e's lowest.
                var type = (TerrainType)BinaryPrimitives.ReverseEndianness(id);
                if (!Enum.IsDefined(type))
                {
                    throw new BuildException($"{settings.TypeName}.Tiles: {id} is no ground tile; use the Tile constants");
                }

                if (!types.Contains(type))
                {
                    types.Add(type);
                }
            }

            // A corner keeps its tile's place in the list in 6 bits since format 12 (patch 2.0.3), in 4 before; the game draws
            // the 64th (probe, 11-13).
            var limit = ground.FormatVersion >= MapEnvironmentFormatVersion.v12 ? 64 : 16;
            if (types.Count > limit)
            {
                var added = string.Join(", ", types.Skip(had).Select(type => Rawcodes.TextOf((int)type)));
                throw new BuildException(
                    $@"{settings.TypeName}.Tiles: the map's tile list would hold {types.Count} tiles, past the {limit} its w3e format holds: {had} from map\war3map.w3e and {types.Count - had} declared ({added}); declare fewer, or remove tiles the map does not use in World Editor");
            }
        }
    }
}
