using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Moonwell.Build
{
    /// <summary>Reads the map's settings by constructing its <c>Moonwell.Map</c> class on .NET.</summary>
    public static class MapSettingsReader
    {
        private const string MapBaseName = "Moonwell.Map";

        // The slots a person or a computer can play, Moonwell.Player.PlayableSlots.
        private const int PlayableSlots = 24;

        // The last Moonwell.Controller and Moonwell.Race a slot can have.
        private const int Neutral = 3;
        private const int NightElf = 3;

        /// <summary>Loads the built assembly, finds the map class, constructs it and reads its settings.</summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        /// <param name="project">The map project; lines in messages are relative to its folder.</param>
        /// <returns>The map's settings.</returns>
        /// <exception cref="BuildException">The assembly declares a Main, has no or several map classes, or the class does not construct.</exception>
        public static MapSettings Read(string assemblyPath, MapProject project)
        {
            if (!File.Exists(assemblyPath))
            {
                throw new BuildException($"the built assembly does not exist: {assemblyPath}");
            }

            var context = new MapLoadContext(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!);
            try
            {
                return ReadIn(context, assemblyPath, project);
            }
            finally
            {
                context.Unload();
            }
        }

        // Kept apart from Read so that nothing of the loaded assembly is on Read's frame when the context unloads.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static MapSettings ReadIn(MapLoadContext context, string assemblyPath, MapProject project)
        {
            var assembly = context.Open(assemblyPath);
            var types = assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (methods.Any(method => method.Name == "Main"))
                {
                    throw new BuildException($"{MapClasses.Describe(type)}.Main: a map declares no Main; the build writes it");
                }
            }

            var maps = types.Where(IsMapClass).ToList();
            if (maps.Count == 0)
            {
                throw new BuildException($"no map class: declare one class that derives from {MapBaseName}");
            }

            if (maps.Count > 1)
            {
                throw new BuildException("more than one map class: " + string.Join(", ", maps.Select(MapClasses.Describe)));
            }

            var mapType = maps[0];
            var name = MapClasses.Describe(mapType);
            if (mapType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new BuildException($"{name} needs a public constructor without parameters");
            }

            object map;
            try
            {
                map = Activator.CreateInstance(mapType)!;
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                throw MapClasses.ConstructionError(wrapped.InnerException, name, project);
            }

            var (slots, teams) = ReadSlots(map, name);
            return new MapSettings(
                name,
                ReadSetting(map, "Name"),
                ReadSetting(map, "Author"),
                ReadSetting(map, "Description"),
                ReadSetting(map, "RecommendedPlayers"),
                ReadNumber(map, "DataSet"),
                ReadNumber(map, "DataVersion"),
                ReadSetting(map, "SaveFolder"),
                ReadSetting(map, "SaveKey"),
                slots,
                teams);
        }

        // The map's Slots, a protected list, each read across the load context by its properties; teams in the order the slots first name them.
        private static (List<SlotSetting> Slots, List<TeamSetting> Teams) ReadSlots(object map, string name)
        {
            var slots = new List<SlotSetting>();
            var teams = new List<TeamSetting>();
            var teamObjects = new List<object>();
            var property = SettingOf(map, "Slots", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = (System.Collections.IEnumerable?)property.GetValue(map);
            if (list == null)
            {
                return (slots, teams);
            }

            foreach (var slot in list)
            {
                if (slot == null)
                {
                    throw new BuildException($"{name}.Slots holds a null slot");
                }

                var number = (int)Value(slot, "Number")!;
                if (number < 0 || number >= PlayableSlots)
                {
                    throw new BuildException($"{name}.Slots: slot {number} is not one a person or a computer plays; slots go from 0 to {PlayableSlots - 1}");
                }

                if (slots.Any(other => other.Number == number))
                {
                    throw new BuildException($"{name}.Slots: slot {number} is set up twice");
                }

                var team = Value(slot, "Team") ?? throw new BuildException($"{name}.Slots: slot {number} has no team");
                var controller = Value(slot, "Controller")!;
                var controllerNumber = Convert.ToInt32(controller, CultureInfo.InvariantCulture);
                if (controllerNumber > Neutral)
                {
                    throw new BuildException($"{name}.Slots: slot {number} cannot be played by {controller}; use User, Computer, Rescuable or Neutral");
                }

                var race = Value(slot, "Race");
                var raceNumber = race == null ? -1 : Convert.ToInt32(race, CultureInfo.InvariantCulture);
                if (raceNumber > NightElf)
                {
                    throw new BuildException($"{name}.Slots: slot {number} cannot play {race}; use Human, Orc, Undead, NightElf, or none for the player's pick");
                }

                var index = teamObjects.IndexOf(team);
                if (index < 0)
                {
                    index = teamObjects.Count;
                    teamObjects.Add(team);
                    teams.Add(new TeamSetting((string)Value(team, "Name")!, (bool)Value(team, "SharedVision")!, (bool)Value(team, "SharedControl")!));
                }

                slots.Add(new SlotSetting(number, index, controllerNumber, raceNumber, (string?)Value(slot, "Name") ?? string.Empty));
            }

            return (slots, teams);
        }

        private static object? Value(object target, string property)
        {
            return target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.GetValue(target);
        }

        private static bool IsMapClass(Type type)
        {
            if (type.IsAbstract)
            {
                return false;
            }

            for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            {
                if (parent.FullName == MapBaseName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadSetting(object map, string property)
        {
            return (string?)SettingOf(map, property).GetValue(map) ?? string.Empty;
        }

        // The enum crosses the load context as its number.
        private static int ReadNumber(object map, string property)
        {
            return Convert.ToInt32(SettingOf(map, property).GetValue(map), CultureInfo.InvariantCulture);
        }

        // Read from the Moonwell.Map base type, so that a property the class hides with new is not asked for.
        private static PropertyInfo SettingOf(object map, string property, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
        {
            var baseType = map.GetType().BaseType!;
            while (baseType.FullName != MapBaseName)
            {
                baseType = baseType.BaseType!;
            }

            return baseType.GetProperty(property, flags)!;
        }
    }
}
