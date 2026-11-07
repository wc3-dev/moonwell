using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Moonwell.Build
{
    /// <summary>
    /// The TOCs the frames package loads when it starts, which the build writes into the map of a map that references it:
    /// the game's template files the kinds are made from, then the map's own .fdf files in its assets, in a TOC of their
    /// own loaded after; each CRLF with a blank last line, as it was seen loading (11-03, 11-05).
    /// </summary>
    internal static class FramesToc
    {
        /// <summary>The file's name in the map; the package loads it by this name (FramesSystem.TocFile).</summary>
        internal const string FileName = "MoonwellFrames.toc";

        /// <summary>The map's TOC's name in the map (FramesSystem.MapTocFile); written only for a map with .fdf files in its assets.</summary>
        internal const string MapFileName = "MoonwellMap.toc";

        private static readonly string[] GameFiles =
        {
            @"UI\FrameDef\UI\EscMenuTemplates.fdf",
            @"UI\FrameDef\Glue\StandardTemplates.fdf",
            @"UI\FrameDef\Glue\BattleNetTemplates.fdf",
            @"UI\FrameDef\UI\QuestDialog.fdf",
        };

        /// <summary>The kit's templates' file in the map, written for a map that references Moonwell.Kit and listed first in the map's TOC, so a map's own .fdf may build on them.</summary>
        internal const string KitFileName = "MoonwellKit.fdf";

        /// <summary>The races Moonwell.Kit's Theme has a window look for, as the game's art folders name them.</summary>
        private static readonly string[] KitRaces = { "Human", "Orc", "NightElf", "Undead" };

        /// <summary>Writes the TOCs into the staged map: the game's, and the map's when the map uses the kit or its assets hold .fdf files, by their paths in the map, the kit's templates first.</summary>
        internal static void Write(string stagedFolder, string assetsFolder, bool kit)
        {
            WriteToc(Path.Combine(stagedFolder, FileName), GameFiles);
            var own = (Directory.Exists(assetsFolder) ? Directory.GetFiles(assetsFolder, "*.fdf", SearchOption.AllDirectories) : Array.Empty<string>())
                .Select(file => Path.GetRelativePath(assetsFolder, file).Replace('/', '\\'))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
            if (kit)
            {
                File.WriteAllBytes(Path.Combine(stagedFolder, KitFileName), Encoding.ASCII.GetBytes(KitTemplates()));
                own.Insert(0, KitFileName);
            }

            if (own.Count > 0)
            {
                WriteToc(Path.Combine(stagedFolder, MapFileName), own.ToArray());
            }
        }

        /// <summary>The kit's templates: a window look for each race (MoonwellKitWindowHuman and the rest, which Moonwell.Kit's Theme names), from the game's art, war3skins.txt's EscMenuBorder and EscMenuBackground, with EscMenuBackdrop's sizes.</summary>
        internal static string KitTemplates()
        {
            var text = new StringBuilder();
            foreach (var race in KitRaces)
            {
                var art = @"UI\Widgets\EscMenu\" + race + @"\" + race.ToLowerInvariant() + "-options-menu-";
                text.Append("Frame \"BACKDROP\" \"MoonwellKitWindow").Append(race).Append("\" {\r\n")
                    .Append("    BackdropTileBackground,\r\n")
                    .Append("    BackdropBackground  \"").Append(art).Append("background.blp\",\r\n")
                    .Append("    BackdropCornerFlags \"UL|UR|BL|BR|T|L|B|R\",\r\n")
                    .Append("    BackdropCornerSize  0.05,\r\n")
                    .Append("    BackdropBackgroundSize  0.256,\r\n")
                    .Append("    BackdropBackgroundInsets 0.01 0.01 0.01 0.01,\r\n")
                    .Append("    BackdropEdgeFile  \"").Append(art).Append("border.blp\",\r\n")
                    .Append("    BackdropBlendAll,\r\n")
                    .Append("}\r\n\r\n");
            }

            return text.ToString();
        }

        private static void WriteToc(string path, string[] files)
        {
            var text = new StringBuilder();
            foreach (var file in files)
            {
                text.Append(file).Append("\r\n");
            }

            text.Append("\r\n");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes(text.ToString()));
        }
    }
}
