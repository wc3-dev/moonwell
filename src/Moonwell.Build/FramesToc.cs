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

        /// <summary>Writes the TOCs into the staged map: the game's, and the map's when its assets hold .fdf files, by their paths in the map.</summary>
        internal static void Write(string stagedFolder, string assetsFolder)
        {
            WriteToc(Path.Combine(stagedFolder, FileName), GameFiles);
            if (!Directory.Exists(assetsFolder))
            {
                return;
            }

            var own = Directory.GetFiles(assetsFolder, "*.fdf", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(assetsFolder, file).Replace('/', '\\'))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();
            if (own.Length > 0)
            {
                WriteToc(Path.Combine(stagedFolder, MapFileName), own);
            }
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
