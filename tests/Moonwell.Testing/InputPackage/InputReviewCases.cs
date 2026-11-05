using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases for input the game's events leave out (a slow first repeat, a player who leaves holding a button) and, in a development build, for the debug mode's halt, keys and clicks beside the map's own.</summary>
    public static class InputReviewCases
    {
        /// <summary>Makes slots 0 and 1 user players, installs the keyboard on a fake world, and returns it.</summary>
        private static FakeKeyWorld Keys(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            var fake = new FakeKeyWorld();
            InputSystem.InstallKeyboard(fake);
            return fake;
        }

        /// <summary>Makes slots 0 and 1 user players, installs the mouse on a fake world, and returns it.</summary>
        private static FakeInputWorld Mice(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            var fake = new FakeInputWorld();
            InputSystem.Install(fake, true, new string[0], new float[0]);
            return fake;
        }

        /// <summary>A held key whose first repeat comes a second after the press (Windows' longest repeat delay, or lag) is still one press.</summary>
        public static void SlowFirstRepeat(FakeWorld world)
        {
            var fake = Keys(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            fake.Press(Player.One, Key.W);
            world.Advance(1.0f);
            fake.Press(Player.One, Key.W);
            world.Advance(0.03f);
            fake.Press(Player.One, Key.W);
            Expect.Equal("game pressed W", string.Join(", ", log), "the log");
        }

        /// <summary>A player who leaves holding mouse buttons holds none from then on.</summary>
        public static void LeaverLetsGoOfButtons(FakeWorld world)
        {
            var fake = Mice(world);
            fake.Press(Player.Two, MouseButton.Left, 0.0f, 0.0f);
            fake.Press(Player.Two, MouseButton.Right, 0.0f, 0.0f);
            fake.Leave(Player.Two);
            var mouse = Player.Two.Mouse();
            Expect.True(!mouse.IsDown(MouseButton.Left) && !mouse.IsDown(MouseButton.Right), "no button down after the leave");
        }

#if DEBUG
        /// <summary>A unit Moonwell never took in is held by Ctrl + H and let go by the resume, and Ctrl + click selects it.</summary>
        public static void HaltHoldsUnknownUnits(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 100.0f, 100.0f);
            world.PlacedUnits.Add(handle);
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            debug.Ctrl(0, DebugKey.H);
            Expect.True(world.IsPausedUnit(handle), "held by the halt");
            debug.Ctrl(0, DebugKey.H);
            Expect.True(!world.IsPausedUnit(handle), "let go by the resume");
            debug.CtrlClick(0, 105.0f, 100.0f);
            Expect.True(DebugSession.Selected != null && ((Unit)DebugSession.Selected).Handle == handle, "selected by Ctrl + click");
        }

        /// <summary>A pause the map sets on a held unit during a halt is the map's: IsPaused reads it, and the resume leaves the unit paused.</summary>
        public static void MapPauseDuringHalt(FakeWorld world)
        {
            var stunned = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            var freed = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            debug.Ctrl(0, DebugKey.H);
            Expect.True(!stunned.IsPaused, "a held unit reads not paused: the hold is the debug mode's");
            stunned.IsPaused = true;
            Expect.True(stunned.IsPaused, "paused by the map during the halt");
            debug.Ctrl(0, DebugKey.H);
            Expect.True(world.IsPausedUnit(stunned.Handle), "still paused after the resume");
            Expect.True(!world.IsPausedUnit(freed.Handle), "the other unit is let go");
        }

        /// <summary>A held key's first repeat, 0.5 s of game time after the press, is a repeat at debug speed 8 too.</summary>
        public static void RepeatAtDebugSpeed(FakeWorld world)
        {
            var fake = Keys(world);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "game"));
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            debug.Chat(0, "-speed 8");
            fake.Press(Player.One, Key.W);
            world.Advance(0.5f);
            fake.Press(Player.One, Key.W);
            Expect.Equal("game pressed W", string.Join(", ", log), "the log");
        }

        /// <summary>The debug mode's keys and its Ctrl + click do not reach the map's key and mouse reactions while it is on.</summary>
        public static void DebugInputIsItsOwn(FakeWorld world)
        {
            var keys = Keys(world);
            var mice = new FakeInputWorld();
            InputSystem.Install(mice, true, new string[0], new float[0]);
            var log = new List<string>();
            Game.Instance.Add(new LogKeyGame(log, "keys"));
            Game.Instance.Add(new LogMouseGame(log, "mouse"));
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            keys.Press(Player.One, Key.H, 2);
            keys.Release(Player.One, Key.H, 2);
            debug.Key(0, DebugKey.Control, 0, true);
            mice.Press(Player.One, MouseButton.Left, 10.0f, 10.0f);
            mice.Release(Player.One, MouseButton.Left, 10.0f, 10.0f);
            debug.Key(0, DebugKey.Control, 0, false);
            Expect.Equal("", string.Join(", ", log), "the map's reactions heard nothing");
            keys.Press(Player.One, Key.Q, 2);
            Expect.Equal("keys pressed Q", string.Join(", ", log), "Ctrl + Q is the map's");
        }
#endif
    }
}
