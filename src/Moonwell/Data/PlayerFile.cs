using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// A file on a player's own computer, in <c>Documents\Warcraft III\CustomMapData\</c> and the map's save folder (decision 179):
    /// written on that player's machine only, read on it at once inside <c>Local.For</c>, or read and shared with every machine
    /// through a scope's <c>ReadFile</c>. A name is letters, digits, <c>-</c>, <c>_</c> and <c>.</c>, ending in <c>.txt</c> or
    /// <c>.pld</c>, the extensions the game reads back.
    /// </summary>
    public static class PlayerFile
    {
        /// <summary>The map's save folder in CustomMapData; the build installs it from the map's settings.</summary>
        internal static string Folder { get; set; } = "Map";

        /// <summary>
        /// Writes a text to a file on the player's computer, replacing what it held; nothing happens on other machines. A file
        /// holds about 40,000 characters; a longer text is reported in development builds and not written.
        /// </summary>
        [LocalSafe]
        public static void Write(Player player, string path, string text)
        {
            if (!Allowed(path, "PlayerFile.Write") || !Runtime.World.IsLocal(player.Handle))
            {
                return;
            }

            var pieces = FileText.Pieces(FileText.Escape(text));
            if (pieces.Count > FileText.MostPieces)
            {
                Dev.Report("PlayerFile.Write", path + " is too long for a file: " + text.Length + " characters");
                return;
            }

            var full = Folder + "\\" + path;
            ReplayGuard.WhenWritable(() => Runtime.World.WritePreload(full, pieces));
        }

        /// <summary>The text of a file on this machine's computer, null when there is none; it differs between machines, so it is read inside Local.For.</summary>
        [MachineLocal]
        public static string? Read(string path)
        {
            if (!Allowed(path, "PlayerFile.Read"))
            {
                return null;
            }

            var pieces = Runtime.World.ReadPreload(Folder + "\\" + path);
            return pieces == null ? null : FileText.Unescape(Joined(pieces));
        }

        private static string Joined(List<string> pieces)
        {
            var text = new StringBuilder();
            for (var i = 0; i < pieces.Count; i++)
            {
                text.Append(pieces[i]);
            }

            return text.ToString();
        }

        private static bool Allowed(string path, string label)
        {
            var named = path.Length > 4 && (path.EndsWith(".txt") || path.EndsWith(".pld"));
            for (var i = 0; named && i < path.Length; i++)
            {
                var c = path[i];
                named = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
            }

            if (!named)
            {
                Dev.Report(label, "\"" + path + "\" is no file name: letters, digits, -, _ and ., ending in .txt or .pld, the extensions the game reads back");
            }

            return named;
        }
    }
}
