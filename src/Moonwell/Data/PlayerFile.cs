using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// A file on a player's own computer, in <c>Documents\Warcraft III\CustomMapData\</c> and the map's save folder (decision 179):
    /// written on that player's machine only, read on it at once inside <c>Local.For</c>, or read and shared with every machine
    /// through a scope's <c>ReadFile</c>. A file is plain text the player can read and change, unless it is written and read
    /// with <c>encrypted: true</c> (decision 398). A name is letters, digits, <c>-</c>, <c>_</c> and <c>.</c>, ending in <c>.txt</c> or
    /// <c>.pld</c>, the extensions the game reads back.
    /// </summary>
    public static class PlayerFile
    {
        /// <summary>The map's save folder in CustomMapData; the build installs it from the map's settings.</summary>
        internal static string Folder { get; set; } = "Map";

        // What an encrypted file's seal is made for, so that a save is no file.
        private const string Kind = "file";

        /// <summary>
        /// Writes a text to a file on the player's computer, replacing what it held; nothing happens on other machines. A file
        /// holds about 40,000 characters; a longer text is reported in development builds and not written. With
        /// <paramref name="encrypted"/> the text is sealed under the map's SaveKey and the player's name (decision 398): it
        /// cannot be read or changed without the key, it holds about 29,000 characters, and it is read back with
        /// <c>encrypted: true</c>.
        /// </summary>
        [LocalSafe]
        public static void Write(Player player, string path, string text, bool encrypted = false)
        {
            const string label = "PlayerFile.Write";
            if (!Allowed(path, label) || (encrypted && !Keyed(label)) || !Runtime.World.IsLocal(player.Handle))
            {
                return;
            }

            var kept = encrypted ? Seal.ToLetters(Seal.Close(Kind, SealName(path), player.StartName, Utf8.Encode(text))) : text;
            var pieces = FileText.Pieces(FileText.Escape(kept));
            if (pieces.Count > FileText.MostPieces)
            {
                Dev.Report(label, path + " is too long for a file: " + text.Length + " characters");
                return;
            }

            var full = Folder + "\\" + path;
            ReplayGuard.WhenWritable(() => Runtime.World.WritePreload(full, pieces));
        }

        /// <summary>
        /// The text of a file on this machine's computer, null when there is none; it differs between machines, so it is read
        /// inside Local.For. With <paramref name="encrypted"/> it reads a file written with <c>encrypted: true</c>: one that
        /// fails its check, as a plain or changed file does, gives null and a line in the log.
        /// </summary>
        [MachineLocal]
        public static string? Read(string path, bool encrypted = false)
        {
            const string label = "PlayerFile.Read";
            if (encrypted && !Keyed(label))
            {
                return null;
            }

            var text = Kept(path);
            if (text == null || !encrypted)
            {
                return text;
            }

            for (var slot = 0; slot < Player.MaxSlots; slot++)
            {
                var player = Player.Get(slot);
                if (Runtime.World.IsLocal(player.Handle))
                {
                    return Opened(player, path, text, label);
                }
            }

            return null;
        }

        /// <summary>Whether the map has a SaveKey for an encrypted file; a development build reports one without.</summary>
        internal static bool Keyed(string label)
        {
            if (!Seal.HasKey)
            {
                Dev.Report(label, "an encrypted file needs the map's SaveKey: set SaveKey = \"a long private text, never changed\" in the map's constructor in Map.cs");
            }

            return Seal.HasKey;
        }

        /// <summary>A file's text as it is kept, sealed or plain, on this machine; null when there is none or the name is no file's.</summary>
        internal static string? Kept(string path)
        {
            if (!Allowed(path, "PlayerFile.Read"))
            {
                return null;
            }

            var pieces = Runtime.World.ReadPreload(Folder + "\\" + path);
            return pieces == null ? null : FileText.Unescape(Joined(pieces));
        }

        /// <summary>The text of a player's encrypted file from what the file kept; null, and a line in the log, when it fails its check.</summary>
        internal static string? Opened(Player player, string path, string kept, string label)
        {
            var closed = Seal.FromLetters(kept);
            var bytes = closed == null ? null : Seal.Open(Kind, SealName(path), player.StartName, closed);
            if (bytes == null)
            {
                Seal.Failed(label, "the file " + path + " of " + player.Name + " fails its check, so it gives null: it was not encrypted, was changed, or was made under another SaveKey, for another name or as another file");
                return null;
            }

            return Utf8.Decode(bytes, 0, bytes.Count);
        }

        // The game finds a file in any case, so a file's seal is made for its name in lower case.
        private static string SealName(string path)
        {
            return path.ToLower();
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
