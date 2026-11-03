using System.IO;
using System.Text;

namespace Moonwell.Build
{
    /// <summary>
    /// The TOC the frames package loads when it starts, which the build writes into the map of a map that references it: the
    /// game's template files the kinds are made from, CRLF and a blank last line, as it was seen loading (11-03).
    /// </summary>
    internal static class FramesToc
    {
        /// <summary>The file's name in the map; the package loads it by this name (FramesSystem.TocFile).</summary>
        internal const string FileName = "MoonwellFrames.toc";

        private static readonly string[] GameFiles =
        {
            @"UI\FrameDef\UI\EscMenuTemplates.fdf",
            @"UI\FrameDef\Glue\StandardTemplates.fdf",
            @"UI\FrameDef\Glue\BattleNetTemplates.fdf",
            @"UI\FrameDef\UI\QuestDialog.fdf",
        };

        /// <summary>Writes the TOC into the staged map.</summary>
        internal static void Write(string stagedFolder)
        {
            var text = new StringBuilder();
            foreach (var file in GameFiles)
            {
                text.Append(file).Append("\r\n");
            }

            text.Append("\r\n");
            File.WriteAllBytes(Path.Combine(stagedFolder, FileName), Encoding.ASCII.GetBytes(text.ToString()));
        }
    }
}
