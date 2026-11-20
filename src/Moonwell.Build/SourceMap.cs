using War3Net.Build;
using War3Net.Build.Info;
using War3Net.Build.Widget;

namespace Moonwell.Build
{
    // War3Net's file flags, not the Moonwell.MapFiles namespace the name would otherwise find first.
    using MapFiles = War3Net.Build.MapFiles;

    /// <summary>Opens the World Editor map the build compiles the script into.</summary>
    public static class SourceMap
    {
        // Only what War3Net's script builder reads and the info file; the other files are copied as they are.
        internal const MapFiles ScriptFiles = MapFiles.Info | MapFiles.Units | MapFiles.UnitObjectData | MapFiles.Doodads
            | MapFiles.Regions | MapFiles.Cameras | MapFiles.Sounds | MapFiles.Environment | MapFiles.TriggerStrings;

        /// <summary>Opens the project's map without its GUI triggers and applies the map class's settings, its game data option and its slots among them.</summary>
        /// <param name="project">The map project.</param>
        /// <param name="settings">The settings read from the map class; empty ones keep World Editor's values.</param>
        /// <param name="log">The build log; a game data option or setting the map class changes, and computers a moonwell run game leaves empty, are noted in it.</param>
        /// <returns>The opened map, its script language set to Lua.</returns>
        public static War3Net.Build.Map Open(MapProject project, MapSettings settings, TextWriter log)
        {
            War3Net.Build.Map map;
            try
            {
                map = War3Net.Build.Map.Open(project.MapFolder, ScriptFiles);
            }
            catch (Exception failure) when (failure is not BuildException)
            {
                throw new BuildException($@"cannot read the World Editor map in {project.MapName}\: {failure.Message}");
            }

            var info = map.Info ?? throw new BuildException($@"no World Editor map: {project.MapName}\war3map.w3i is missing");
            CheckLayouts(map, info, project.MapName);
            info.ScriptLanguage = ScriptLanguage.Lua;
            var set = info.GameDataSet;
            var version = info.GameDataVersion;
            var data = GameData.Of(settings.DataSet, settings.DataVersion);
            data.Apply(info);
            if (info.GameDataSet != set || info.GameDataVersion != version)
            {
                log.WriteLine($"note: the map's game data option {GameData.Describe(set, version)} is replaced by {data}");
            }

            var texts = MapTypes.TextsOf(map.TriggerStrings);
            info.MapName = Replace(info.MapName, settings.Name, "name", texts, log);
            info.MapAuthor = Replace(info.MapAuthor, settings.Author, "author", texts, log);
            info.MapDescription = Replace(info.MapDescription, settings.Description, "description", texts, log);
            info.RecommendedPlayers = Replace(info.RecommendedPlayers, settings.RecommendedPlayers, "recommended players", texts, log);

            MapSetup.Apply(map, settings, log, project.MapName);
            MapSetup.ApplyTiles(map, settings, project.MapName);
            NoteEmptyComputers(info, log);

            return map;
        }

        // A setting the map class leaves empty keeps World Editor's value; one that replaces a different text is noted.
        private static string? Replace(string? held, string set, string what, IReadOnlyDictionary<uint, string> texts, TextWriter log)
        {
            if (set.Length == 0)
            {
                return held;
            }

            var text = MapTypes.TextOf(held ?? string.Empty, texts);
            if (text.Length > 0 && text != set)
            {
                log.WriteLine($"note: the map's {what} \"{text.ReplaceLineEndings(" ")}\" in World Editor is replaced by the map class's \"{set.ReplaceLineEndings(" ")}\"");
            }

            return set;
        }

        // With custom forces and no fixed player settings, a -loadfile game seats none of the map's computers (Sven's runs, 11-20).
        private static void NoteEmptyComputers(MapInfo info, TextWriter log)
        {
            if (!info.MapFlags.HasFlag(MapFlags.UseCustomForces) || info.MapFlags.HasFlag(MapFlags.FixedPlayerSettingsForCustomForces))
            {
                return;
            }

            var computers = info.Players.Where(player => player.Controller == PlayerController.Computer).Select(player => player.Id).ToList();
            if (computers.Count > 0)
            {
                log.WriteLine($"note: {(computers.Count == 1 ? "slot" : "slots")} {string.Join(", ", computers)} {(computers.Count == 1 ? "is a computer" : "are computers")} in World Editor, but a moonwell run game leaves {(computers.Count == 1 ? "it" : "them")} empty, as the map has custom forces without fixed player settings; set up Slots in the map class, or turn on Fixed Player Settings in World Editor's forces");
            }
        }

        /// <summary>
        /// Stops the build at placed doodads or units in a layout the game reads wrongly with the map's <c>war3map.w3i</c>: one naming
        /// game 1.32 or later makes it read a skin id after each one's scale, an older one none (J139). A file holding none passes.
        /// </summary>
        /// <exception cref="BuildException"><c>war3map.doo</c> or <c>war3mapUnits.doo</c> is in the other layout.</exception>
        internal static void CheckLayouts(War3Net.Build.Map map, MapInfo info, string mapName)
        {
            var skins = info.GameVersion is { } version && version >= new Version(1, 32);
            var game = info.GameVersion is { } named ? $"names game {named.Major}.{named.Minor}" : $"names no game version (format {(int)info.FormatVersion})";
            Check(map.Doodads is { Doodads.Count: > 0 } doodads ? doodads.UseNewFormat : null, MapDoodads.FileName, "doodads");
            Check(map.Units is { Units.Count: > 0 } units ? units.UseNewFormat : null, MapUnits.FileName, "units");

            void Check(bool? hasSkins, string file, string what)
            {
                if (hasSkins is { } has && has != skins)
                {
                    var layout = has ? "has skin ids (the layout of 1.32 on)" : "has no skin ids (the layout before 1.32)";
                    var wanted = skins ? "with" : "without";
                    throw new BuildException(
                        $@"{mapName}\{file} {layout}, but {mapName}\war3map.w3i {game}, so the game reads it {wanted} them and places its {what} wrong; "
                        + $"write the {what} again {wanted} skin ids, the layout that war3map.w3i wants (the two files likely come from different maps)");
                }
            }
        }
    }
}
