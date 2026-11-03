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
