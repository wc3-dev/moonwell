using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for chat commands (decision 189): a scope's Command hears "-name" lines in any case, with the rest trimmed.</summary>
    public static class ChatCases
    {
        /// <summary>A command hears its line from any person, with the player and the rest of the line, trimmed.</summary>
        public static void Heard(FakeWorld world)
        {
            People(world, 2);
            var heard = new List<string>();
            Game.Instance.Command("load", (player, rest) => heard.Add(player.Slot + ":" + rest));
            world.Chat(Player.Two, "-load  x7kq M3PA ");
            world.Chat(Player.One, "-load");
            Expect.Equal(2, heard.Count, "two lines heard");
            Expect.Equal("1:x7kq M3PA", heard[0], "player two's code, trimmed");
            Expect.Equal("0:", heard[1], "no rest");
        }

        /// <summary>The name matches in any case and as a whole word; a line that does not start with the dash is no command, though the game's event gives every line with one.</summary>
        public static void OnlyItsWord(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            Game.Instance.Command("Load", (player, rest) => heard.Add(rest));
            world.Chat(Player.One, "-LOAD a");
            world.Chat(Player.One, "-loader b");
            world.Chat(Player.One, "load c");
            world.Chat(Player.One, "-");
            world.Chat(Player.One, "- load d");
            world.Chat(Player.One, "hi -load e");
            world.Chat(Player.One, "xload -f");
            world.Chat(Player.One, " -load g");
            Expect.Equal(1, heard.Count, "one line heard");
            Expect.Equal("a", heard[0], "-LOAD");
        }

        /// <summary>A command ends with its scope, or by its own End.</summary>
        public static void Ends(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            var scope = new TestScope("scope", new List<string>());
            Game.Instance.Adopt(scope);
            scope.Command("load", (player, rest) => heard.Add("scope"));
            var command = Game.Instance.Command("zoom", (player, rest) => heard.Add("zoom"));
            scope.End();
            command.End();
            world.Chat(Player.One, "-load x");
            world.Chat(Player.One, "-zoom 2000");
            Expect.Equal(0, heard.Count, "nothing heard");
        }

        /// <summary>Two commands of one name both run, the older first; one that throws is reported and the next still runs.</summary>
        public static void TwoOfOneName(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            Game.Instance.Command("zoom", (player, rest) => heard.Add("first " + rest));
            Game.Instance.Command("zoom", (player, rest) => throw new System.InvalidOperationException("broken"));
            Game.Instance.Command("ZOOM", (player, rest) => heard.Add("third " + rest));
            world.Chat(Player.One, "-zoom 2000");
            Expect.Equal(2, heard.Count, "both ran");
            Expect.Equal("first 2000", heard[0], "the older first");
            Expect.Equal("third 2000", heard[1], "then the newer, past the one that threw");
            Expect.Equal(1, world.Printed.Count, "the error is reported");
            Expect.True(world.Logged[0].Contains("-zoom") && world.Logged[0].Contains("broken"), "the report: " + world.Logged[0]);
        }

        /// <summary>An ended command is let go at once, and not only at the next line with a dash, so what its handler holds is not kept.</summary>
        public static void EndedLetGo(FakeWorld world)
        {
            People(world, 1);
            var command = Game.Instance.Command("a", (player, rest) => { });
            Game.Instance.Command("b", (player, rest) => { });
            command.End();
            Expect.Equal(1, ChatCommand.Count, "one kept");
        }

        /// <summary>A command a handler makes waits for the next line, and one it ends runs no more, on this line or after.</summary>
        public static void MadeAndEndedByAHandler(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            ChatCommand? second = null;
            var made = false;
            Game.Instance.Command("go", (player, rest) =>
            {
                heard.Add("first " + rest);
                second!.End();
                if (!made)
                {
                    made = true;
                    Game.Instance.Command("go", (p, r) => heard.Add("made " + r));
                }
            });
            second = Game.Instance.Command("go", (player, rest) => heard.Add("second " + rest));
            world.Chat(Player.One, "-go 1");
            world.Chat(Player.One, "-go 2");
            Expect.Equal(3, heard.Count, "three ran");
            Expect.Equal("first 1", heard[0], "the first line: only the first, which ended the second");
            Expect.Equal("first 2", heard[1], "the second line: the first");
            Expect.Equal("made 2", heard[2], "then the one made on the first line");
        }

        /// <summary>A bad name is reported and refused in every build: the command given back has ended and hears nothing.</summary>
        public static void BadNameRefused(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            var empty = Game.Instance.Command("", (player, rest) => heard.Add("empty"));
            var dashed = Game.Instance.Command("-load", (player, rest) => heard.Add("dashed"));
            var spaced = Game.Instance.Command("a b", (player, rest) => heard.Add("spaced"));
            world.Chat(Player.One, "- hello");
            world.Chat(Player.One, "--load");
            world.Chat(Player.One, "-a b");
            Expect.Equal(0, heard.Count, "nothing heard");
            Expect.True(empty.HasEnded && dashed.HasEnded && spaced.HasEnded, "each given back ended");
            Expect.Equal(3, world.Printed.Count, "each reported");
            foreach (var line in world.Logged)
            {
                Expect.True(line.Contains("is no command name"), "the report: " + line);
            }
        }

        /// <summary>Chat is listened for only from the first command on, once for each person.</summary>
        public static void ListensOnce(FakeWorld world)
        {
            People(world, 2);
            world.SetSlot(2, SlotState.Playing, Controller.Computer);
            Expect.Equal(0, world.ListenedChat.Count, "nothing before a command");
            Game.Instance.Command("a", (player, rest) => { });
            Game.Instance.Command("b", (player, rest) => { });
            Expect.Equal(2, world.ListenedChat.Count, "the two people, once each");
        }

#if DEBUG
        /// <summary>A name a debug command has is reported in a development build, and still made: release has no debug commands.</summary>
        public static void DebugNameReported(FakeWorld world)
        {
            People(world, 1);
            var heard = new List<string>();
            Game.Instance.Command("Stats", (player, rest) => heard.Add(rest));
            Game.Instance.Command("load", (player, rest) => { });
            world.Chat(Player.One, "-stats");
            Expect.Equal(1, world.Logged.Count, "reported once");
            Expect.True(world.Logged[0].Contains("-stats"), "the report: " + world.Logged[0]);
            Expect.Equal(1, heard.Count, "still heard");
        }
#endif

        private static void People(FakeWorld world, int count)
        {
            for (var slot = 0; slot < count; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
            }
        }
    }
}
