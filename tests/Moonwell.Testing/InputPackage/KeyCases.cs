using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for each player's keyboard: the keys, the state the events keep, the reactions, and the release of a key let go unseen, on the fake game.</summary>
    public static class KeyCases
    {
        /// <summary>Makes slots 0 and 1 user players, installs the keyboard on a fake world, and returns it.</summary>
        private static FakeKeyWorld Fresh(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            var fake = new FakeKeyWorld();
            InputSystem.InstallKeyboard(fake);
            return fake;
        }

        /// <summary>A key's number is the game's code for it.</summary>
        public static void Codes(FakeWorld world)
        {
            Expect.Equal(65, (int)Key.A, "A");
            Expect.Equal(48, (int)Key.D0, "D0");
            Expect.Equal(96, (int)Key.NumPad0, "NumPad0");
            Expect.Equal(160, (int)Key.LeftShift, "LeftShift");
            Expect.Equal(192, (int)Key.Oem3, "Oem3");
            Expect.Equal(254, (int)Key.OemClear, "OemClear");
        }

        /// <summary>A press makes the key down for that player only, and a release up again.</summary>
        public static void DownAndUp(FakeWorld world)
        {
            var fake = Fresh(world);
            var keyboard = Player.One.Keyboard();
            Expect.True(!keyboard.IsDown(Key.W), "not down before an event");
            fake.Press(Player.One, Key.W);
            Expect.True(keyboard.IsDown(Key.W), "down after the press");
            Expect.True(!Player.Two.Keyboard().IsDown(Key.W), "another player's key is not down");
            Expect.True(!keyboard.IsDown(Key.S), "another key is not down");
            fake.Release(Player.One, Key.W);
            Expect.True(!keyboard.IsDown(Key.W), "up after the release");
            Expect.Same(Player.One, keyboard.Player, "the keyboard's player");
        }

        /// <summary>Shift, Control and Alt are what the last key event's modifiers said.</summary>
        public static void Modifiers(FakeWorld world)
        {
            var fake = Fresh(world);
            var keyboard = Player.One.Keyboard();
            Expect.True(!keyboard.Shift && !keyboard.Control && !keyboard.Alt, "none before an event");
            fake.Press(Player.One, Key.Q, 1);
            Expect.True(keyboard.Shift, "Shift with meta 1");
            Expect.True(!keyboard.Control, "no Control with meta 1");
            fake.Press(Player.One, Key.LeftCtrl, 3);
            Expect.True(keyboard.Shift && keyboard.Control, "Shift and Control with meta 3");
            fake.Release(Player.One, Key.LeftCtrl, 1);
            Expect.True(!keyboard.Control, "Control up with meta 1");
            Expect.True(keyboard.Shift, "Shift still with meta 1");
            fake.Press(Player.One, Key.E, 4);
            Expect.True(keyboard.Alt && !keyboard.Shift, "Alt alone with meta 4");
            Expect.True(!Player.Two.Keyboard().Alt, "another player's modifiers are not touched");
        }

        /// <summary>IsHeldNow reads this machine's keyboard, whichever player's keyboard it is read from.</summary>
        public static void HeldNow(FakeWorld world)
        {
            var fake = Fresh(world);
            fake.Held[87] = true;
            Expect.True(Player.One.Keyboard().IsHeldNow(Key.W), "held on this machine");
            Expect.True(Player.Two.Keyboard().IsHeldNow(Key.W), "read from another player's keyboard too");
            Expect.True(!Player.One.Keyboard().IsDown(Key.W), "held now is not down by the events");
            fake.Held[87] = false;
            Expect.True(!Player.One.Keyboard().IsHeldNow(Key.W), "let go");
        }

        /// <summary>A code no key has changes nothing and throws nothing; asking for a number no key has reads false.</summary>
        public static void UnknownCode(FakeWorld world)
        {
            var fake = Fresh(world);
            var keyboard = Player.One.Keyboard();
            fake.Send(Player.One, 0, 1, true);
            fake.Send(Player.One, 255, 2, true);
            fake.Send(Player.One, 300, 4, true);
            fake.Send(Player.One, -1, 0, true);
            Expect.True(!keyboard.Shift && !keyboard.Control && !keyboard.Alt, "their modifiers are not taken");
            Expect.True(!keyboard.IsDown((Key)300), "300 is not down");
            Expect.True(!keyboard.IsDown((Key)(-1)), "-1 is not down");
            fake.Press(Player.One, Key.W);
            Expect.True(keyboard.IsDown(Key.W), "and a real key still works");
        }

        /// <summary>A keyboard read before the install throws.</summary>
        public static void BeforeInstall(FakeWorld world)
        {
            InputSystem.Uninstall();
            Expect.Throws<InvalidOperationException>(() => Player.One.Keyboard(), "a read before the install");
        }

        /// <summary>Fails unless the log is exactly these lines.</summary>
        private static void ExpectLog(List<string> log, string[] expected, string what)
        {
            Expect.Equal(string.Join(", ", expected), string.Join(", ", log.ToArray()), what);
        }

        /// <summary>A key event of Player 1 runs the game's components in add order, then Player 1's, never Player 2's.</summary>
        public static void Order(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game a"));
            Player.One.Add(new LogKeyPlayer(log, "player"));
            Player.Two.Add(new LogKeyPlayer(log, "player"));
            Game.Instance.Add(new LogKeyGame(log, "game b"));
            fake.Press(Player.One, Key.W);
            ExpectLog(log, new[] { "game a pressed W", "game b pressed W", "player pressed W of 1" }, "a press of Player 1");
            fake.Press(Player.Two, Key.W);
            log.Clear();
            fake.Release(Player.Two, Key.W);
            ExpectLog(log, new[] { "game a released W", "game b released W", "player released W of 2" }, "a release of Player 2");
            log.Clear();
            fake.Release(Player.Two, Key.W);
            ExpectLog(log, new string[0], "a release of a key already up runs nothing");
        }

        /// <summary>A held key's repeats (the game's first after 0.5 s, then about 30 a second) run no reaction, and the key stays down.</summary>
        public static void OncePerPress(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(0.5f);
            fake.Press(Player.One, Key.W);
            world.Advance(0.03f);
            fake.Press(Player.One, Key.W);
            world.Advance(0.03f);
            fake.Press(Player.One, Key.W);
            ExpectLog(log, new[] { "game pressed W" }, "one press for the hold");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "still down");
            fake.Release(Player.One, Key.W);
            ExpectLog(log, new[] { "game pressed W", "game released W" }, "and its release");
        }

        /// <summary>A press long after the key's last one, with no release between (a release the game did not send), is a new press: its release runs first.</summary>
        public static void LongGap(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(1.0f);
            fake.Press(Player.One, Key.W);
            ExpectLog(log, new[] { "game pressed W", "game released W", "game pressed W" }, "released, then pressed again");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "down");
        }

        /// <summary>A held Ctrl repeats with meta 2: one press, and Control stays set.</summary>
        public static void ModifierRepeat(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            var keyboard = Player.One.Keyboard();
            fake.Press(Player.One, Key.LeftCtrl, 2);
            world.Advance(0.5f);
            fake.Press(Player.One, Key.LeftCtrl, 2);
            Expect.True(keyboard.Control, "Control after the first repeat");
            world.Advance(0.03f);
            fake.Press(Player.One, Key.LeftCtrl, 2);
            ExpectLog(log, new[] { "game pressed LeftCtrl" }, "one press");
            Expect.True(keyboard.Control, "Control throughout");
        }

        /// <summary>A watcher added by a watcher's reaction runs from the next event, and one ended earlier in the same event does not run.</summary>
        public static void ChangedInReaction(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            var late = new LogKeyGame(log, "late");
            Game.Instance.Add(new AddingKeys(log, new LogKeyGame(log, "added"), late));
            Game.Instance.Add(late);
            fake.Press(Player.One, Key.Q);
            ExpectLog(log, new[] { "adder pressed" }, "the added one waits, and the ended one is not called");
            log.Clear();
            fake.Release(Player.One, Key.Q);
            fake.Press(Player.One, Key.Q);
            ExpectLog(log, new[] { "added released Q", "adder pressed", "added pressed Q" }, "the added one from the next event");
        }

        /// <summary>A player's watcher added by a game watcher's reaction runs from the next event too.</summary>
        public static void AddedToPlayerInReaction(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new AddingPlayerKeys(new LogKeyPlayer(log, "added")));
            fake.Press(Player.One, Key.Q);
            ExpectLog(log, new string[0], "the added one waits");
            fake.Release(Player.One, Key.Q);
            ExpectLog(log, new[] { "added released Q of 1" }, "the added one from the next event");
        }

        /// <summary>A reaction that throws is reported as Class.Method, and the next component still runs, with the keyboard already up to date.</summary>
        public static void Throwing(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new ThrowingKeys());
            Game.Instance.Add(new LogKeyGame(log, "after"));
            fake.Press(Player.One, Key.W);
            Expect.Equal(1, world.Logged.Count, "one line logged");
            Expect.Equal("Moonwell: ThrowingKeys.OnKeyPressed failed: boom", world.Logged[0], "the logged line");
            ExpectLog(log, new[] { "after pressed W" }, "the next component ran");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "the key was down");
        }

        /// <summary>A component that writes only OnKeyPressed is called on a press and not on a release.</summary>
        public static void PressOnly(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new PressOnlyKeys(log));
            fake.Press(Player.One, Key.A);
            fake.Release(Player.One, Key.A);
            ExpectLog(log, new[] { "press only pressed A" }, "the press only");
        }

        /// <summary>A key the events say is down and this machine no longer holds is sent at the next check, a quarter second; when the message arrives, the key comes up and OnKeyReleased runs.</summary>
        public static void LetGoUnseen(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(0.25f);
            Expect.Equal("0 87 1", string.Join(", ", fake.Sent.ToArray()), "the message sent");
            fake.Deliver();
            ExpectLog(log, new[] { "game pressed W", "game released W" }, "released by the message");
            Expect.True(!Player.One.Keyboard().IsDown(Key.W), "up");
        }

        /// <summary>A key this machine still holds sends nothing.</summary>
        public static void StillHeld(FakeWorld world)
        {
            var fake = Fresh(world);
            fake.Held[87] = true;
            fake.Press(Player.One, Key.W);
            world.Advance(1.0f);
            Expect.Equal(0, fake.Sent.Count, "nothing sent");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "still down");
        }

        /// <summary>A message for a press the key has since been let go and pressed again after releases nothing: it carries the press it was sent for.</summary>
        public static void DownAgain(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(0.25f);
            fake.Release(Player.One, Key.W);
            fake.Press(Player.One, Key.W);
            fake.Deliver();
            ExpectLog(log, new[] { "game pressed W", "game released W", "game pressed W" }, "the old message releases nothing");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "down again");
        }

        /// <summary>A key pressed again soon after the message was sent, which the keyboard takes as a repeat, is not released by it: any down after the send voids the message.</summary>
        public static void RepeatAfterSend(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(0.25f);
            Expect.Equal("0 87 1", string.Join(", ", fake.Sent.ToArray()), "the message sent");
            fake.Held[87] = true;
            fake.Press(Player.One, Key.W);
            fake.Deliver();
            ExpectLog(log, new[] { "game pressed W" }, "nothing released");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "still down");
        }

        /// <summary>A key pressed soon after the message released it is a new press.</summary>
        public static void PressAfterRelease(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(0.25f);
            fake.Deliver();
            world.Advance(0.05f);
            fake.Press(Player.One, Key.W);
            ExpectLog(log, new[] { "game pressed W", "game released W", "game pressed W" }, "released by the message, then pressed again");
        }

        /// <summary>A message for a key already up runs nothing.</summary>
        public static void AlreadyUp(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Arrive(Player.One, 87, 0);
            Expect.Equal(0, log.Count, "nothing ran");
        }

        /// <summary>A modifier key released by a message takes its modifier off, unless the other side's key is still down.</summary>
        public static void ModifierLetGo(FakeWorld world)
        {
            var fake = Fresh(world);
            var keyboard = Player.One.Keyboard();
            fake.Press(Player.One, Key.LeftShift, 1);
            fake.Press(Player.One, Key.RightShift, 1);
            fake.Arrive(Player.One, (int)Key.LeftShift, 1);
            Expect.True(keyboard.Shift, "the right Shift is still down");
            fake.Arrive(Player.One, (int)Key.RightShift, 1);
            Expect.True(!keyboard.Shift, "no Shift down");
            fake.Press(Player.One, Key.LeftCtrl, 2);
            fake.Arrive(Player.One, (int)Key.LeftCtrl, 1);
            Expect.True(!keyboard.Control, "Control off");
        }

        /// <summary>A player who left is not checked.</summary>
        public static void LeftPlayer(FakeWorld world)
        {
            var fake = Fresh(world);
            fake.Press(Player.One, Key.W);
            world.SetSlot(0, SlotState.Left, Controller.User);
            world.Advance(0.5f);
            Expect.Equal(0, fake.Sent.Count, "nothing sent for a player who left");
        }

        /// <summary>A player who leaves holds no keys and no modifiers from then on, and no reaction runs for the keys let go.</summary>
        public static void LeaverHoldsNothing(FakeWorld world)
        {
            var fake = Fresh(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            var keyboard = Player.One.Keyboard();
            fake.Press(Player.One, Key.W);
            fake.Press(Player.One, Key.LeftShift, 1);
            fake.Press(Player.Two, Key.W);
            log.Clear();
            fake.Leave(Player.One);
            Expect.True(!keyboard.IsDown(Key.W) && !keyboard.IsDown(Key.LeftShift), "no key down");
            Expect.True(!keyboard.Shift, "no modifier");
            Expect.Equal(0, log.Count, "no reaction ran");
            Expect.True(Player.Two.Keyboard().IsDown(Key.W), "another player keeps their keys");
            world.Advance(0.5f);
            Expect.True(!fake.Sent.Exists(line => line.StartsWith("0 ")), "nothing sent for the leaver");
        }

        /// <summary>A message releases the key of the player who sent it, not another player's.</summary>
        public static void OnlySender(FakeWorld world)
        {
            var fake = Fresh(world);
            fake.Held[87] = true;
            fake.Press(Player.One, Key.W);
            fake.Press(Player.Two, Key.W);
            fake.Arrive(Player.Two, 87, 1);
            Expect.True(!Player.Two.Keyboard().IsDown(Key.W), "the sender's W is up");
            Expect.True(Player.One.Keyboard().IsDown(Key.W), "the other player's W is still down");
        }

        /// <summary>The keyboard listens for the people playing at the install, in slot order: not a computer.</summary>
        public static void PeopleOnly(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            world.SetSlot(2, SlotState.Playing, Controller.Computer);
            var fake = new FakeKeyWorld();
            InputSystem.InstallKeyboard(fake);
            Expect.Equal("0, 1", string.Join(", ", fake.ListenedSlots.ToArray()), "the slots listened for");
        }
    }
}
