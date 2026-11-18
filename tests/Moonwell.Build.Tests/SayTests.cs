using Moonwell.Tool;

namespace Moonwell.Build.Tests
{
    public sealed class SayTests
    {
        [Fact]
        public void outside_a_terminal_each_kind_of_line_is_plain_text()
        {
            var written = new StringWriter { NewLine = "\n" };
            var say = new Say(written);

            say.Result(@"Made C:\Maps\[Next]");
            say.Message("note", @"no build\ to delete");
            say.Message("warning", "nothing names these:", items: new[] { "a.blp", "b.blp" });
            say.Message("error", "Next is not empty");
            say.Line("[bold]H001[/]  [dim]hero[/]");

            Assert.False(say.Styled);
            Assert.Equal(
                "✓ Made C:\\Maps\\[Next]\n" +
                "note     no build\\ to delete\n" +
                "warning  nothing names these: a.blp, b.blp\n" +
                "error    Next is not empty\n" +
                "H001  hero\n",
                written.ToString());
        }

        [Fact]
        public void the_build_librarys_log_lines_are_notes_and_warnings()
        {
            var written = new StringWriter { NewLine = "\n" };
            using (var log = new Say(written).Log())
            {
                log.WriteLine("moonwell: imported old.w3x: its files are in map");
                log.WriteLine("moonwell: warning: war3map.j looks protected");
                log.WriteLine("note: slot 1 has no start location");
                log.WriteLine();
            }

            Assert.Equal(
                "note     imported old.w3x: its files are in map\n" +
                "warning  war3map.j looks protected\n" +
                "note     slot 1 has no start location\n",
                written.ToString());
        }
    }
}
