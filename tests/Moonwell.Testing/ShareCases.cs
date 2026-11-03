using System.Collections.Generic;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>Cases for the sharing piece: what one machine knows, sent to every machine as sync messages.</summary>
    public static class ShareCases
    {
        /// <summary>A short answer from this machine's player arrives a tick later, once.</summary>
        public static void ShortAnswer(FakeWorld world)
        {
            People(world, 2);
            var got = new List<string?>();
            Sharing.Request(Game.Instance, Player.One, () => "hi", text => got.Add(text));
            Expect.Equal(0, got.Count, "nothing before the message arrives");
            world.Advance(0.1f);
            Expect.Equal(1, got.Count, "arrived once");
            Expect.Equal("hi", got[0], "the answer");
        }

        /// <summary>A long answer goes out as several messages, none longer than a sync message holds, and arrives whole.</summary>
        public static void LongAnswer(FakeWorld world)
        {
            People(world, 1);
            var answer = Repeat("abcdefghij", 60);
            string? got = null;
            Sharing.Request(Game.Instance, Player.One, () => answer, text => got = text);
            world.Advance(0.1f);
            Expect.True(world.SentSync.Count >= 3, "sent in 3 messages or more, not " + world.SentSync.Count);
            for (var i = 0; i < world.SentSync.Count; i++)
            {
                Expect.True(world.SentSync[i].Length <= Sharing.MessageLength, "message " + i + " within 255");
            }

            Expect.Equal(answer, got, "the answer");
        }

        /// <summary>A zero byte, which would end a sync message, and the escape's own byte arrive as they were.</summary>
        public static void ZeroByte(FakeWorld world)
        {
            People(world, 1);
            var answer = "a\0b\u0001c";
            string? got = null;
            Sharing.Request(Game.Instance, Player.One, () => answer, text => got = text);
            world.Advance(0.1f);
            for (var i = 0; i < world.SentSync.Count; i++)
            {
                Expect.True(world.SentSync[i].IndexOf('\0') < 0, "no zero byte sent");
            }

            Expect.Equal(answer, got, "the answer");
        }

        /// <summary>Null and the empty text arrive as themselves.</summary>
        public static void NullAndEmpty(FakeWorld world)
        {
            People(world, 1);
            var got = new List<string?>();
            Sharing.Request(Game.Instance, Player.One, () => null, text => got.Add(text));
            Sharing.Request(Game.Instance, Player.One, () => "", text => got.Add(text));
            world.Advance(0.1f);
            Expect.Equal(2, got.Count, "both arrived");
            Expect.True(got[0] == null, "null");
            Expect.Equal("", got[1], "the empty text");
        }

        /// <summary>On another player's machine the read does not run, and that machine's messages bring the answer.</summary>
        public static void OtherMachine(FakeWorld world)
        {
            People(world, 2);
            world.LocalSlot = 1;
            var read = false;
            string? got = null;
            Sharing.Request(Game.Instance, Player.One, () =>
            {
                read = true;
                return "mine";
            }, text => got = text);
            foreach (var message in Sharing.Messages(1, "from one"))
            {
                world.DeliverSync(Player.One, message);
            }

            Expect.True(!read, "the read ran on player one's machine only");
            Expect.Equal(0, world.SentSync.Count, "this machine sent nothing");
            Expect.Equal("from one", got, "the answer");
        }

        /// <summary>Two requests for one player and one for another, their pieces arriving interleaved: each gets its own answer.</summary>
        public static void TwoInFlight(FakeWorld world)
        {
            People(world, 3);
            world.LocalSlot = 2;
            var got = new string?[3];
            Sharing.Request(Game.Instance, Player.One, () => "", text => got[0] = text);
            Sharing.Request(Game.Instance, Player.One, () => "", text => got[1] = text);
            Sharing.Request(Game.Instance, Player.Two, () => "", text => got[2] = text);
            var a = Sharing.Messages(1, Repeat("a", 600));
            var b = Sharing.Messages(2, Repeat("b", 600));
            var c = Sharing.Messages(3, Repeat("c", 600));
            for (var i = 0; i < a.Count; i++)
            {
                world.DeliverSync(Player.One, a[i]);
                world.DeliverSync(Player.Two, c[i]);
                world.DeliverSync(Player.One, b[i]);
            }

            Expect.Equal(Repeat("a", 600), got[0], "the first");
            Expect.Equal(Repeat("b", 600), got[1], "the second");
            Expect.Equal(Repeat("c", 600), got[2], "player two's");
        }

        /// <summary>A message from another player than the request's is not taken as its answer.</summary>
        public static void WrongSender(FakeWorld world)
        {
            People(world, 3);
            world.LocalSlot = 2;
            string? got = "not yet";
            Sharing.Request(Game.Instance, Player.One, () => "", text => got = text);
            foreach (var message in Sharing.Messages(1, "from two"))
            {
                world.DeliverSync(Player.Two, message);
            }

            Expect.Equal("not yet", got, "no answer from another player");
        }

        /// <summary>A scope that ends before the answer: the callback never runs.</summary>
        public static void ScopeEnded(FakeWorld world)
        {
            People(world, 1);
            var scope = new TestScope("scope", new List<string>());
            Game.Instance.Adopt(scope);
            var ran = false;
            Sharing.Request(scope, Player.One, () => "late", text => ran = true);
            scope.End();
            world.Advance(0.1f);
            Expect.True(!ran, "no callback");
        }

        /// <summary>A player who leaves halfway through a long answer: the callback gets null.</summary>
        public static void LeaveHalfway(FakeWorld world)
        {
            People(world, 2);
            world.LocalSlot = 0;
            var got = new List<string?>();
            Sharing.Request(Game.Instance, Player.Two, () => "", text => got.Add(text));
            var messages = Sharing.Messages(1, Repeat("x", 600));
            world.DeliverSync(Player.Two, messages[0]);
            world.SetSlot(1, SlotState.Left, Controller.User);
            Inbound.Left(world.PlayerAt(1));
            Expect.Equal(1, got.Count, "answered once");
            Expect.True(got[0] == null, "with null");
        }

        /// <summary>A request for a computer: null in the next instant, and no read.</summary>
        public static void Computer(FakeWorld world)
        {
            People(world, 1);
            world.SetSlot(1, SlotState.Playing, Controller.Computer);
            var read = false;
            var got = new List<string?>();
            Sharing.Request(Game.Instance, Player.Two, () =>
            {
                read = true;
                return "never";
            }, text => got.Add(text));
            Expect.Equal(0, got.Count, "not at once");
            world.Advance(0.01f);
            Expect.Equal(1, got.Count, "in the next instant");
            Expect.True(got[0] == null, "with null");
            Expect.True(!read, "no read");
        }

        private static void People(FakeWorld world, int count)
        {
            for (var slot = 0; slot < count; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
            }
        }

        private static string Repeat(string piece, int count)
        {
            var text = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                text.Append(piece);
            }

            return text.ToString();
        }
    }
}
