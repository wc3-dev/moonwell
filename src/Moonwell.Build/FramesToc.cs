using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>The labels' text templates' file in the map, written for every map that references the frames package and listed first in the map's TOC.</summary>
        internal const string TextFileName = "MoonwellText.fdf";

        /// <summary>The game's font by path: a template's FrameFont takes a font only by path (seen 11-13).</summary>
        private const string GameFont = @"Fonts\FRIZQT__.TTF";

        /// <summary>The races Moonwell.Kit's Theme has a window look for, as the game's art folders name them.</summary>
        private static readonly string[] KitRaces = { "Human", "Orc", "NightElf", "Undead" };

        /// <summary>Writes the TOCs into the staged map: the game's, and the map's, by their paths in the map: the labels' text templates, then the kit's when the map uses the kit, then the .fdf files in its assets.</summary>
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

            var fonts = (Directory.Exists(assetsFolder) ? Directory.GetFiles(assetsFolder, "*.ttf", SearchOption.AllDirectories) : Array.Empty<string>())
                .Select(file => Path.GetRelativePath(assetsFolder, file).Replace('/', '\\'))
                .OrderBy(file => file, StringComparer.Ordinal);
            File.WriteAllBytes(Path.Combine(stagedFolder, TextFileName), Encoding.ASCII.GetBytes(TextTemplates(fonts)));
            own.Insert(0, TextFileName);
            WriteToc(Path.Combine(stagedFolder, MapFileName), own.ToArray());
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

        /// <summary>The labels' text templates (Label.Size, Font and Shadow): a TEXT for each height from 0.005 to 0.04 in steps of 0.0005, with and without a shadow, for the game's font and each font file given; named as Label.TextTemplate names them, FrameFont first in each, as some fonts are said to need.</summary>
        internal static string TextTemplates(IEnumerable<string> fontPaths)
        {
            var text = new StringBuilder();
            AddTextTemplates(text, "Game", GameFont);
            foreach (var path in fontPaths)
            {
                AddTextTemplates(text, FontKey(path), path);
            }

            return text.ToString();
        }

        private static void AddTextTemplates(StringBuilder text, string key, string path)
        {
            for (var step = 10; step <= 80; step++)
            {
                var height = (step * 0.0005).ToString("0.0###", CultureInfo.InvariantCulture);
                foreach (var shadow in new[] { false, true })
                {
                    text.Append("Frame \"TEXT\" \"MoonwellText_").Append(key).Append('_').Append(step).Append(shadow ? "S" : "").Append("\" {\r\n")
                        .Append("    FrameFont \"").Append(path).Append("\", ").Append(height).Append(", \"\",\r\n")
                        .Append("    FontColor 1.0 1.0 1.0 1.0,\r\n");
                    if (shadow)
                    {
                        text.Append("    FontShadowColor 0.0 0.0 0.0 1.0,\r\n")
                            .Append("    FontShadowOffset 0.002 -0.002,\r\n");
                    }

                    text.Append("}\r\n\r\n");
                }
            }
        }

        /// <summary>A font path as a template name takes it, as Label does: letters lowered, all but letters and digits an underscore.</summary>
        private static string FontKey(string path)
        {
            return new string(path.ToLowerInvariant().Select(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_').ToArray());
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
