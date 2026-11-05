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

        /// <summary>The name matches in any case and as a whole word; a line without the dash is no command.</summary>
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
        /// <summary>A name with the dash or a space is reported in a development build.</summary>
        public static void BadNameReported(FakeWorld world)
        {
            People(world, 1);
            Game.Instance.Command("-load", (player, rest) => { });
            Expect.Equal(1, world.Logged.Count, "reported");
            Expect.True(world.Logged[0].Contains("Command(\\\"load\\\")") || world.Logged[0].Contains("Command(\"load\")"), "the report: " + world.Logged[0]);
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
