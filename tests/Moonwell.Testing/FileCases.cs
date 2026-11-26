using System.Collections.Generic;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>Cases for a player's files: the text's escapes and pieces, and PlayerFile (decision 179).</summary>
    public static class FileCases
    {
        private const string Awkward = "say \"hi\"\n\ttab ~ ~q | \\ Åsa ö é € 中 😀 \r\0end~";

        /// <summary>Any text comes back from its escaped form as it was.</summary>
        public static void EscapeRoundTrip(FakeWorld world)
        {
            Expect.Equal(Awkward, FileText.Unescape(FileText.Escape(Awkward)), "the awkward text");
            Expect.Equal("", FileText.Unescape(FileText.Escape("")), "the empty text");
        }

        /// <summary>
        /// The escaped form has no quote, which would end the JASS string, no backslash, which the game writes doubled so that
        /// a piece's bytes in the file could pass Preload's 259, and no byte below 32.
        /// </summary>
        public static void EscapedIsSafe(FakeWorld world)
        {
            var escaped = FileText.Escape(Awkward);
            for (var i = 0; i < escaped.Length; i++)
            {
                Expect.True(escaped[i] != '"' && escaped[i] != '\\' && escaped[i] >= 32, "no quote, backslash or control byte at " + i);
            }

            Expect.Equal("a~qb~~c~0Ad~5Ce", FileText.Escape("a\"b~c\nd\\e"), "the escapes");
        }

        /// <summary>Pieces are 200 bytes at most, never end inside a letter, and join to the text.</summary>
        public static void Pieces(FakeWorld world)
        {
            var text = new StringBuilder();
            for (var i = 0; i < 199; i++)
            {
                text.Append('a');
            }

            text.Append("éb");
            for (var i = 0; i < 300; i++)
            {
                text.Append(i % 2 == 0 ? "中" : "x");
            }

            var pieces = FileText.Pieces(text.ToString());
            var joined = new StringBuilder();
            for (var i = 0; i < pieces.Count; i++)
            {
                Expect.True(pieces[i].Length <= FileText.PieceBytes, "piece " + i + " within 200");
                Expect.True(pieces[i].Length == 0 || (pieces[i][0] & 0xC0) != 0x80, "piece " + i + " starts a letter");
                joined.Append(pieces[i]);
            }

            Expect.Equal(text.ToString(), joined.ToString(), "the pieces joined");
            if ("å".Length == 2)
            {
                Expect.Equal(199, pieces[0].Length, "in the game's bytes the first piece stops before é");
            }
        }

        /// <summary>The empty text is one empty piece, so a file of it reads back as the empty text.</summary>
        public static void EmptyPieces(FakeWorld world)
        {
            var pieces = FileText.Pieces("");
            Expect.Equal(1, pieces.Count, "one piece");
            Expect.Equal("", pieces[0], "empty");
        }

        /// <summary>Text where no letter starts in a whole piece's length is still cut, at the piece's length.</summary>
        public static void NoLetterStart(FakeWorld world)
        {
            var inside = "å".Length == 2 ? (char)0x80 : (char)0xDC00;
            var text = new StringBuilder();
            for (var i = 0; i < 401; i++)
            {
                text.Append(inside);
            }

            var pieces = FileText.Pieces(text.ToString());
            var joined = new StringBuilder();
            for (var i = 0; i < pieces.Count; i++)
            {
                Expect.True(pieces[i].Length <= FileText.PieceBytes, "piece " + i + " within 200");
                joined.Append(pieces[i]);
            }

            Expect.Equal(text.ToString(), joined.ToString(), "the pieces joined");
        }

        /// <summary>The longest line a file holds, the extended tooltip of level 99 with a whole piece, keeps within Preload's 259 bytes.</summary>
        public static void LongestLineFits(FakeWorld world)
        {
            var piece = new StringBuilder();
            for (var i = 0; i < FileText.PieceBytes; i++)
            {
                piece.Append('x');
            }

            Expect.True(FileText.Line(199, piece.ToString()).Length <= 259, "the line of piece 199");
        }

        /// <summary>This machine's player writes a file under the save folder, and Read inside Local.For gives the text back.</summary>
        public static void WriteThenRead(FakeWorld world)
        {
            People(world, 1);
            PlayerFile.Write(Player.One, "settings.txt", Awkward);
            Expect.True(world.Files.ContainsKey("Map\\settings.txt"), "written under the save folder");
            string? got = null;
            Local.For(Player.One, () => { got = PlayerFile.Read("settings.txt"); });
            Expect.Equal(Awkward, got, "the text read back");
        }

        /// <summary>The folder the build installs is the one files go in.</summary>
        public static void InstalledFolder(FakeWorld world)
        {
            People(world, 1);
            Runtime.InstallSaveFolder("MyRpg");
            PlayerFile.Write(Player.One, "a.pld", "x");
            Expect.True(world.Files.ContainsKey("MyRpg\\a.pld"), "written under MyRpg");
        }

        /// <summary>Another player's write leaves this machine without a file.</summary>
        public static void OtherPlayersFile(FakeWorld world)
        {
            People(world, 2);
            PlayerFile.Write(Player.Two, "settings.txt", "theirs");
            Expect.Equal(0, world.Files.Count, "nothing written here");
        }

        /// <summary>A missing file reads as null.</summary>
        public static void MissingFile(FakeWorld world)
        {
            People(world, 1);
            string? got = "not read";
            Local.For(Player.One, () => { got = PlayerFile.Read("nothing.txt"); });
            Expect.True(got == null, "null");
        }

        /// <summary>A path with a folder or another extension is reported in development builds and writes nothing.</summary>
        public static void BadPath(FakeWorld world)
        {
            People(world, 1);
            PlayerFile.Write(Player.One, "a/b.txt", "x");
            PlayerFile.Write(Player.One, "b.json", "x");
            Expect.Equal(0, world.Files.Count, "nothing written");
#if DEBUG
            Expect.Equal(2, world.Logged.Count, "both reported");
            Expect.True(world.Logged[0].Contains("\"a/b.txt\" is no file name"), "the report: " + world.Logged[0]);
            Expect.True(world.Logged[1].Contains("PlayerFile.Write") && world.Logged[1].Contains(".txt or .pld"), "the report: " + world.Logged[1]);
#endif
        }

        /// <summary>A text longer than a file holds is reported in development builds and writes nothing.</summary>
        public static void TooLong(FakeWorld world)
        {
            People(world, 1);
            var text = new StringBuilder();
            for (var i = 0; i <= FileText.PieceBytes * FileText.MostPieces; i++)
            {
                text.Append('a');
            }

            PlayerFile.Write(Player.One, "big.txt", text.ToString());
            Expect.Equal(0, world.Files.Count, "nothing written");
#if DEBUG
            Expect.Equal(1, world.Logged.Count, "reported");
            Expect.True(world.Logged[0].Contains("is too long for a file"), "the report: " + world.Logged[0]);
#endif
        }

        /// <summary>With the replay guard started, a write waits until this machine's own value has come back, then happens.</summary>
        public static void WriteWaitsForEcho(FakeWorld world)
        {
            People(world, 1);
            ReplayGuard.Start();
            PlayerFile.Write(Player.One, "settings.txt", "kept");
            Expect.Equal(0, world.Files.Count, "not yet written");
            world.Advance(0.1f);
            Expect.True(world.Files.ContainsKey("Map\\settings.txt"), "written once the value came back");
        }

        /// <summary>In a replay the value that comes back is the recorded one, and nothing is written.</summary>
        public static void ReplayWritesNothing(FakeWorld world)
        {
            People(world, 1);
            world.RealSeconds = 2000;
            ReplayGuard.Start();
            world.DropPendingSync();
            foreach (var message in Sharing.Messages(1, "1000"))
            {
                world.DeliverSync(Player.One, message);
            }

            PlayerFile.Write(Player.One, "settings.txt", "old");
            world.Advance(0.1f);
            Expect.True(ReplayGuard.IsReplay, "a replay");
            Expect.Equal(0, world.Files.Count, "nothing written");
        }

        /// <summary>A scope's ReadFile reads this machine's player's file and hands the text over once it has gone round.</summary>
        public static void ReadFileHere(FakeWorld world)
        {
            People(world, 1);
            PlayerFile.Write(Player.One, "settings.txt", Awkward);
            string? got = "not yet";
            Game.Instance.ReadFile(Player.One, "settings.txt", text => got = text);
            Expect.Equal("not yet", got, "not before the message goes round");
            world.Advance(0.1f);
            Expect.Equal(Awkward, got, "the text");
        }

        /// <summary>On another machine nothing is read there, and the player's machine's messages bring the text.</summary>
        public static void ReadFileFromAnotherMachine(FakeWorld world)
        {
            People(world, 2);
            world.LocalSlot = 1;
            string? got = "not yet";
            Game.Instance.ReadFile(Player.One, "settings.txt", text => got = text);
            foreach (var message in Sharing.Messages(1, "theirs"))
            {
                world.DeliverSync(Player.One, message);
            }

            Expect.Equal("theirs", got, "player one's text");
        }

        /// <summary>A missing file arrives as null.</summary>
        public static void ReadFileMissing(FakeWorld world)
        {
            People(world, 1);
            string? got = "not yet";
            Game.Instance.ReadFile(Player.One, "nothing.txt", text => got = text);
            world.Advance(0.1f);
            Expect.True(got == null, "null");
        }

        /// <summary>
        /// An encrypted file keeps Base64 letters that show none of its text, and reads back on this machine with
        /// <c>encrypted: true</c>.
        /// </summary>
        public static void EncryptedRoundTrip(FakeWorld world)
        {
            People(world, 1);
            SaveGame.Key = "the case key";
            PlayerFile.Write(Player.One, "unlocks.txt", Awkward, encrypted: true);
            var kept = Kept(world, "Map\\unlocks.txt");
            Expect.True(Seal.FromLetters(kept) != null && !kept.Contains("tab") && !kept.Contains("end"), "letters that show no text: " + kept);
            string? got = null;
            string? plain = "not read";
            Local.For(Player.One, () =>
            {
                got = PlayerFile.Read("unlocks.txt", encrypted: true);
                plain = PlayerFile.Read("unlocks.txt");
            });
            Expect.Equal(Awkward, got, "the text read back");
            Expect.Equal(kept, plain, "read plain, it is the letters");

            // The game finds a file in any case; the fake world's files are found by their exact name, so the copy stands for it.
            if (world.Files.TryGetValue("Map\\unlocks.txt", out var pieces))
            {
                world.Files["Map\\Unlocks.txt"] = new List<string>(pieces);
            }

            Local.For(Player.One, () => { got = PlayerFile.Read("Unlocks.txt", encrypted: true); });
            Expect.Equal(Awkward, got, "read under its name in another case");
            Expect.Equal(0, world.Logged.Count, "nothing logged");

            PlayerFile.Write(Player.One, "empty.txt", "", encrypted: true);
            Local.For(Player.One, () => { got = PlayerFile.Read("empty.txt", encrypted: true); });
            Expect.Equal("", got, "the empty text");
        }

        /// <summary>A scope's ReadFile shares an encrypted file as it is kept, and every machine opens it: here, and on another machine from the player's messages.</summary>
        public static void EncryptedShared(FakeWorld world)
        {
            People(world, 2);
            SaveGame.Key = "the case key";
            PlayerFile.Write(Player.One, "unlocks.txt", Awkward, encrypted: true);
            var kept = Kept(world, "Map\\unlocks.txt");
            string? got = "not yet";
            Game.Instance.ReadFile(Player.One, "unlocks.txt", text => got = text, encrypted: true);
            Expect.Equal("not yet", got, "not before the message goes round");
            world.Advance(0.1f);
            Expect.Equal(Awkward, got, "the text here");
            var sent = new StringBuilder();
            foreach (var message in world.SentSync)
            {
                sent.Append(message);
            }

            Expect.True(sent.ToString().Contains(kept.Substring(0, 40)) && !sent.ToString().Contains("tab"), "sent as it is kept");

            world.LocalSlot = 1;
            got = "not yet";
            Game.Instance.ReadFile(Player.One, "unlocks.txt", text => got = text, encrypted: true);
            foreach (var message in Sharing.Messages(2, kept))
            {
                world.DeliverSync(Player.One, message);
            }

            Expect.Equal(Awkward, got, "the text on another machine");

            got = "not yet";
            Game.Instance.ReadFile(Player.One, "nothing.txt", text => got = text, encrypted: true);
            foreach (var message in Sharing.Messages(3, null))
            {
                world.DeliverSync(Player.One, message);
            }

            Expect.True(got == null, "no file is null");
            Expect.Equal(0, world.Logged.Count, "nothing logged");
        }

        /// <summary>An encrypted file changed by hand, and a plain file read as an encrypted one, give null and a line in the log.</summary>
        public static void EncryptedChanged(FakeWorld world)
        {
            People(world, 1);
            SaveGame.Key = "the case key";
            PlayerFile.Write(Player.One, "unlocks.txt", "gold 100", encrypted: true);
            // Only the writing player's machine has the file; at another the read gives null for no file.
            if (world.Files.TryGetValue("Map\\unlocks.txt", out var pieces))
            {
                pieces[0] = pieces[0].Substring(0, 12) + (pieces[0][12] == 'A' ? 'B' : 'A') + pieces[0].Substring(13);
            }

            string? got = "not read";
            Local.For(Player.One, () => { got = PlayerFile.Read("unlocks.txt", encrypted: true); });
            Expect.True(got == null, "a changed letter");
            Expect.True(world.Logged.Count == 1 && world.Logged[0].Contains("unlocks.txt") && world.Logged[0].Contains("fails its check"), "one line in the log, naming the file and its check");

            PlayerFile.Write(Player.One, "plain.txt", "gold 9999");
            got = "not yet";
            Game.Instance.ReadFile(Player.One, "plain.txt", text => got = text, encrypted: true);
            world.Advance(0.1f);
            Expect.True(got == null, "a plain file");
            Expect.Equal(2, world.Logged.Count, "a second line in the log");
        }

        /// <summary>An encrypted file is its player's and its name's: a copy under another name, another player's copy and one made under another key each fail.</summary>
        public static void EncryptedNotItsOwn(FakeWorld world)
        {
            People(world, 2);
            SaveGame.Key = "the case key";
            PlayerFile.Write(Player.One, "unlocks.txt", "gold 100", encrypted: true);
            if (world.Files.TryGetValue("Map\\unlocks.txt", out var pieces))
            {
                world.Files["Map\\other.txt"] = new List<string>(pieces);
            }

            string? got = "not read";
            Local.For(Player.One, () => { got = PlayerFile.Read("other.txt", encrypted: true); });
            Expect.True(got == null, "under another name");

            world.LocalSlot = 1;
            got = "not read";
            Local.For(Player.Two, () => { got = PlayerFile.Read("unlocks.txt", encrypted: true); });
            Expect.True(got == null, "as another player");

            world.LocalSlot = 0;
            SaveGame.Key = "another key";
            got = "not read";
            Local.For(Player.One, () => { got = PlayerFile.Read("unlocks.txt", encrypted: true); });
            Expect.True(got == null, "under another key");
            Expect.Equal(3, world.Logged.Count, "each in the log");
        }

        /// <summary>Without a SaveKey an encrypted file is not written or read, and a development build says so.</summary>
        public static void EncryptedNoKey(FakeWorld world)
        {
            People(world, 1);
            PlayerFile.Write(Player.One, "unlocks.txt", "gold 100", encrypted: true);
            Expect.Equal(0, world.Files.Count, "nothing written");
            string? got = "not read";
            Local.For(Player.One, () => { got = PlayerFile.Read("unlocks.txt", encrypted: true); });
            Expect.True(got == null, "null");
            got = "not yet";
            Game.Instance.ReadFile(Player.One, "unlocks.txt", text => got = text, encrypted: true);
            world.Advance(0.1f);
            Expect.True(got == null, "null through ReadFile");
#if DEBUG
            Expect.Equal(3, world.Logged.Count, "each reported");
            Expect.True(world.Logged[0].Contains("PlayerFile.Write") && world.Logged[0].Contains("SaveKey"), "the report: " + world.Logged[0]);
#endif
        }

        private static string Kept(FakeWorld world, string path)
        {
            // Only the writing player's machine has the file.
            var text = new StringBuilder();
            if (world.Files.TryGetValue(path, out var pieces))
            {
                foreach (var piece in pieces)
                {
                    text.Append(piece);
                }
            }

            return text.ToString();
        }

        private static void People(FakeWorld world, int count)
        {
            for (var slot = 0; slot < count; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
            }
        }
    }
}
