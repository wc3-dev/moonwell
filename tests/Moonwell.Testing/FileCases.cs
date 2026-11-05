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

        private static void People(FakeWorld world, int count)
        {
            for (var slot = 0; slot < count; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
            }
        }
    }
}
