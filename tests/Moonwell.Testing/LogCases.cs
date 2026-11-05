using Moonwell.Engine;

namespace Moonwell.Testing
{
    /// <summary>Cases for the log: a map's own lines, and the lines and timing of the log file the game world writes.</summary>
    public static class LogCases
    {
        /// <summary>Game.Log writes the map's line to the log as it is.</summary>
        public static void MapLine(FakeWorld world)
        {
            Game.Log("wave 3 started: \"Ghouls\"");
            Expect.Equal(1, world.Logged.Count, "logged lines");
            Expect.Equal("wave 3 started: \"Ghouls\"", world.Logged[0], "the line");
        }

        /// <summary>The file keeps the last 200 lines.</summary>
        public static void KeepsLastLines(FakeWorld world)
        {
            var file = new LogFile();
            for (var i = 1; i <= 201; i++)
            {
                file.Add("line " + i, 0.0f);
            }

            Expect.Equal(200, file.Lines.Count, "the lines kept");
            Expect.Equal("line 2", file.Lines[0], "the first kept");
            Expect.Equal("line 201", file.Lines[199], "the last kept");
        }

        /// <summary>A line longer than one Preload call keeps (253 characters with its wrapping) is split into pieces.</summary>
        public static void SplitsLongLines(FakeWorld world)
        {
            var file = new LogFile();
            var line = Repeat("a", 253) + Repeat("b", 253) + "c";
            file.Add(line, 0.0f);
            Expect.Equal(3, file.Lines.Count, "the pieces");
            Expect.Equal(Repeat("a", 253), file.Lines[0], "the first piece");
            Expect.Equal(Repeat("b", 253), file.Lines[1], "the second piece");
            Expect.Equal("c", file.Lines[2], "the third piece");
        }

        /// <summary>A piece never ends inside a letter of more than one byte: in the game, where a string is its UTF-8 bytes, the cut moves back to the letter's start.</summary>
        public static void SplitsBetweenLetters(FakeWorld world)
        {
            var file = new LogFile();
            var line = Repeat("a", 252) + "åb";
            file.Add(line, 0.0f);
            Expect.Equal(2, file.Lines.Count, "the pieces");
            Expect.Equal(line, file.Lines[0] + file.Lines[1], "the pieces joined");
            if ("å".Length == 2)
            {
                Expect.Equal(Repeat("a", 252), file.Lines[0], "the first piece, in bytes");
                Expect.Equal("åb", file.Lines[1], "the second piece, in bytes");
            }
            else
            {
                Expect.Equal(Repeat("a", 252) + "å", file.Lines[0], "the first piece, in .NET's characters");
                Expect.Equal("b", file.Lines[1], "the second piece, in .NET's characters");
            }
        }

        /// <summary>A long line of letters .NET keeps in one character and the game in two bytes (°) is cut, between letters in the game.</summary>
        public static void SplitsLettersOfTwoBytes(FakeWorld world)
        {
            var file = new LogFile();
            var line = Repeat("°", 300);
            file.Add(line, 0.0f);
            Expect.Equal(line, Joined(file), "the pieces joined");
        }

        /// <summary>Text where no letter starts in a whole piece's length is still cut, at the piece's length.</summary>
        public static void SplitsWithNoLetterStart(FakeWorld world)
        {
            var file = new LogFile();
            var inside = "å".Length == 2 ? (char)0x80 : (char)0xDC00;
            var line = Repeat(inside.ToString(), 600);
            file.Add(line, 0.0f);
            Expect.Equal(3, file.Lines.Count, "the pieces");
            Expect.Equal(LogFile.PieceLength, file.Lines[0].Length, "the first piece, a whole piece long");
            Expect.Equal(line, Joined(file), "the pieces joined");
        }

        /// <summary>A line is written at once when the file was last written a second ago or more; within the second it waits, and is due once the second is up.</summary>
        public static void WritesAtMostOnceASecond(FakeWorld world)
        {
            var file = new LogFile();
            Expect.Equal(true, file.Add("first", 5.0f), "the first line is written at once");
            file.Written(5.0f);
            Expect.Equal(false, file.Add("second", 5.2f), "a line 0.2 s later waits");
            Expect.Equal(false, file.Add("third", 5.5f), "a line 0.5 s later waits");
            Expect.Equal(false, file.Due(5.9f), "not due 0.9 s after the write");
            Expect.Equal(true, file.Due(6.0f), "due 1 s after the write");
            file.Written(6.0f);
            Expect.Equal(false, file.Due(7.5f), "nothing waits after the write");
            Expect.Equal(true, file.Add("fourth", 7.5f), "a line 1.5 s after the write is written at once");
        }

        /// <summary>With the replay guard started, the file waits for this machine's value to come back; in a replay it is never written.</summary>
        public static void WaitsForTheReplayGuard(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            ReplayGuard.Start();
            var file = new LogFile();
            Expect.Equal(false, file.Add("first", 5.0f), "waits for the guard");
            Expect.Equal(false, file.Due(6.0f), "not due before the guard knows");
            world.Advance(0.1f);
            Expect.Equal(true, file.Due(6.0f), "due once the value came back the same");
        }

        /// <summary>In a replay the log file is never written: watching the replay would put its lines over the game's.</summary>
        public static void NotWrittenInAReplay(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.RealSeconds = 2000;
            ReplayGuard.Start();
            world.DropPendingSync();
            foreach (var message in Sharing.Messages(1, "1000"))
            {
                world.DeliverSync(Player.One, message);
            }

            var file = new LogFile();
            Expect.Equal(false, file.Add("first", 5.0f), "not written at once");
            Expect.Equal(false, file.Due(9.0f), "never due");
        }

        private static string Joined(LogFile file)
        {
            var joined = "";
            for (var i = 0; i < file.Lines.Count; i++)
            {
                Expect.True(file.Lines[i].Length <= LogFile.PieceLength, "piece " + i + " within a Preload call");
                joined += file.Lines[i];
            }

            return joined;
        }

        private static string Repeat(string text, int count)
        {
            var result = "";
            for (var i = 0; i < count; i++)
            {
                result += text;
            }

            return result;
        }
    }
}
