using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Moonwell.Testing
{
#if DEBUG
    /// <summary>A game component that only holds what the cases make in it.</summary>
    public sealed class LeakHolder : GameComponent
    {
    }

    /// <summary>A unit component whose OnAdded takes 3 ms of the fake debug world's clock, for the profiler's reactions.</summary>
    public sealed class SlowPart : UnitComponent
    {
        /// <summary>The fake debug world whose clock OnAdded moves on.</summary>
        public static FakeDebugWorld? Debug;

        protected override void OnAdded()
        {
            Debug!.ClockSeconds += 0.003f;
        }
    }

    /// <summary>Cases for the debug tools of 0.11 (decisions 261 to 265): leak tracking, the profiler, the panel's sections, the cheats and the map's own debug commands.</summary>
    public static class ToolsCases
    {
        /// <summary>A unit of the footman type taken in at a place.</summary>
        private static Unit UnitAt(FakeWorld world, float x, float y)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, x, y);
            return Unit.Of(handle)!;
        }

        /// <summary>Installs the fake debug world and turns the mode on with -debug.</summary>
        private static FakeDebugWorld On(FakeWorld world)
        {
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            return debug;
        }

        private static void Nothing()
        {
        }

        /// <summary>Whether any logged line holds the text.</summary>
        private static int Logged(FakeWorld world, string text)
        {
            var found = 0;
            for (var i = 0; i < world.Logged.Count; i++)
            {
                // IndexOf, not Contains: the fork's CoreSystem 2.0.3-moonwell.12 starts Contains's search past the start when the text holds a Lua magic character.
                if (world.Logged[i].IndexOf(text) >= 0)
                {
                    found++;
                }
            }

            return found;
        }

        /// <summary>The last line logged, for a failure's message.</summary>
        private static string Last(FakeWorld world)
        {
            return world.Logged.Count > 0 ? world.Logged[world.Logged.Count - 1] : "nothing logged";
        }

        /// <summary>Whether a line of the panel holds the text.</summary>
        private static bool Shows(FakeDebugWorld debug, string text)
        {
            var lines = debug.PanelLines();
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].IndexOf(text) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A full collection: Lua's, which the host keeps for the cases, or .NET's.</summary>
        /// @CSharpLua.Template = "LuaHostCollectGarbage('collect')"
        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>True in Lua, where the profiler wraps the map's reactions.</summary>
        /// @CSharpLua.Template = "true"
        private static bool InLua()
        {
            return false;
        }

        /// <summary>Makes units and removes them, keeping them in the list when one is given; out of line, so .NET keeps no reference to them in the case.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MakeAndRemove(FakeWorld world, int count, List<Unit>? kept)
        {
            for (var i = 0; i < count; i++)
            {
                var unit = UnitAt(world, i * 10.0f, 0.0f);
                kept?.Add(unit);
                unit.Remove();
            }
        }

        /// <summary>A scope is reported when it holds 500 children, with what it holds, and again at 1,000, not between (decision 262).</summary>
        public static void ScopeGrows(FakeWorld world)
        {
            var holder = Game.Instance.Add(new LeakHolder());
            for (var i = 0; i < 499; i++)
            {
                holder.After(1000.0f, Nothing);
            }

            Expect.Equal(0, Logged(world, "LeakHolder.Leak"), "reports at 499");
            holder.After(1000.0f, Nothing);
            Expect.Equal(1, Logged(world, "Moonwell: LeakHolder.Leak failed: holds 500 things it has not ended (Timer 500)"), "the report at 500: " + Last(world));
            for (var i = 0; i < 499; i++)
            {
                holder.After(1000.0f, Nothing);
            }

            Expect.Equal(1, Logged(world, "LeakHolder.Leak"), "reports at 999");
            holder.After(1000.0f, Nothing);
            Expect.Equal(1, Logged(world, "holds 1,000 things it has not ended (Timer 1,000)"), "the report at 1,000");
        }

        /// <summary>
        /// Ended units a list still holds count once a canary dropped after their end has been collected and a minute has
        /// passed; those let go of don't; 100 are reported with their class (decision 262).
        /// </summary>
        public static void HeldCounted(FakeWorld world)
        {
            var kept = new List<Unit>();
            MakeAndRemove(world, 100, kept);
            MakeAndRemove(world, 50, null);
            world.Advance(10.5f);
            Collect();
            world.Advance(10.0f);
            Expect.Equal(0, Leaks.Held, "held before a minute");
            world.Advance(40.0f);
            Expect.Equal(100, Leaks.Held, "held a minute on");
            Expect.Equal("Unit 100", HeldObjects.Summary(), "by class");
            Expect.Equal(1, Logged(world, "Moonwell: Moonwell.Leak failed: 100 objects that ended over a minute ago are still held by code (Unit 100)"), "the report: " + Last(world));
            Expect.Equal(100, kept.Count, "the list still holds them");
        }

        /// <summary>The profiler times a timer's runs and a map's section, shows them in the panel, and writes every line to profile.txt at its stop (decision 263).</summary>
        public static void ProfileTimersAndSections(FakeWorld world)
        {
            var debug = On(world);
            debug.Chat(0, "-profile");
            Expect.True(Profiler.On, "on after -profile");
            Expect.True(Shows(debug, "PROFILE  running"), "the panel's profile");
            Game.Instance.Every(1.0f, () => debug.ClockSeconds += 0.002f);
            debug.Advance(3.0f);
            DebugMode.StartTiming("Pathing");
            debug.ClockSeconds += 0.005f;
            DebugMode.StopTiming("Pathing");
            DebugMode.StopTiming("Pathing");
            debug.Chat(0, "-profile");
            Expect.True(!Profiler.On, "off after -profile again");
            var lines = debug.Files[Profiler.FilePath];
            Expect.True(lines[0].StartsWith("Moonwell profile: "), "the file's heading");
            Expect.Equal("what | calls | total ms | ms a second | average us", lines[1], "the columns");
            // The sweep, Moonwell's own timer every 0.25 s, took no time, so its line comes last.
            Expect.Equal(5, lines.Count, "the lines");
            Expect.True(lines[2].StartsWith("Every 1.0 s at ") || lines[2].StartsWith("Moonwell: Game, every 1.0 s"), "the timer's line names it: " + lines[2]);
            Expect.True(lines[2].IndexOf(" | 3 | 6.0 | ") >= 0, "three runs, 6 ms: " + lines[2]);
            Expect.True(lines[3].StartsWith("Pathing | 1 | 5.0 | "), "the section, once, 5 ms: " + lines[3]);
            Expect.Equal("0.03", Profiler.Seconds(1.0f / 32.0f), "1/32 s in hundredths");
            Expect.Equal("0.1", Profiler.Seconds(0.1f), "0.1 s in tenths");
            Expect.True(lines[4].StartsWith("Moonwell: Game, every ") && lines[4].IndexOf(" | 0.0 | ") >= 0, "the sweep: " + lines[4]);
        }

        /// <summary>In Lua the profiler wraps a map's class's reactions while it runs, and puts them back at its stop.</summary>
        public static void ProfileReactions(FakeWorld world)
        {
            var debug = On(world);
            SlowPart.Debug = debug;
            var unit = UnitAt(world, 0.0f, 0.0f);
            unit.Add(new SlowPart());
            debug.Chat(0, "-profile");
            unit.Add(new SlowPart());
            debug.Chat(0, "-profile");
            unit.Add(new SlowPart());
            if (!InLua())
            {
                return;
            }

            var lines = debug.Files[Profiler.FilePath];
            Expect.True(lines[2].StartsWith("SlowPart.OnAdded | 1 | 3.0 | "), "the reaction, once, 3 ms: " + lines[2]);
        }

        /// <summary>The new commands answer only while the mode is on; a map's command of the same name wins, with a report; a map's debug command is heard all game and -help lists it (decisions 261 and 265).</summary>
        public static void MapCommandWins(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var debug = FakeDebugWorld.Install(world);
            var player = Player.Get(0);
            var gold = player.Gold;
            debug.Chat(0, "-gold 100");
            Expect.Equal(gold, player.Gold, "-gold while off");
            var waves = 0;
            DebugMode.Command("wave", "starts the next wave", (who, rest) => waves++);
            world.Chat(Player.Get(0), "-wave");
            Expect.Equal(1, waves, "the map's debug command while off");
            debug.Chat(0, "-debug");
            debug.Chat(0, "-gold 100");
            Expect.Equal(gold + 100, player.Gold, "-gold while on");
            var heard = 0;
            Game.Instance.Command("gold", (who, rest) => heard++);
            Expect.Equal(1, Logged(world, "-gold is also a debug command: in a development build this one runs and the debug one steps aside"), "the clash's report");
            world.Chat(Player.Get(0), "-gold 100");
            debug.Chat(0, "-gold 100");
            Expect.Equal(1, heard, "the map's -gold");
            Expect.Equal(gold + 100, player.Gold, "the debug -gold stepped aside");
            debug.Chat(0, "-help");
            Expect.True(Shows(debug, "-wave: starts the next wave"), "-help lists the map's debug command");
            Expect.True(Shows(debug, "The map's: -gold"), "-help lists the map's own commands");
        }

        /// <summary>The cheats on the selection and the typer: -kill, -heal, -god, -spawn, -lumber, -find (decision 265).</summary>
        public static void Cheats(FakeWorld world)
        {
            var debug = On(world);
            var unit = UnitAt(world, 100.0f, 0.0f);
            debug.Chat(0, "-kill");
            Expect.True(unit.IsAlive, "-kill with nothing selected");
            Expect.True(world.PrintedTexts[world.PrintedTexts.Count - 1].IndexOf("Select a unit first") >= 0, "-kill asks for a selection");
            debug.CtrlClick(0, 100.0f, 0.0f);
            unit.Life = 10.0f;
            debug.Chat(0, "-heal");
            Expect.Near(unit.MaxLife, unit.Life, "-heal");
            debug.Chat(0, "-god");
            Expect.True(debug.Invulnerable.Contains(unit.Handle), "-god");
            debug.Chat(0, "-god");
            Expect.True(!debug.Invulnerable.Contains(unit.Handle), "-god again");
            var before = KnownUnits.Live.Count;
            debug.Chat(0, "-spawn hfoo 3");
            Expect.Equal(before + 3, KnownUnits.Live.Count, "-spawn hfoo 3");
            var lumber = Player.Get(0).Lumber;
            debug.Chat(0, "-lumber 50");
            Expect.Equal(lumber + 50, Player.Get(0).Lumber, "-lumber 50");
            debug.Chat(0, "-find unit");
            Expect.True(DebugMode.Selected != null && DebugMode.Selected != unit, "-find selects the next unit");
            debug.CtrlClick(0, 100.0f, 0.0f);
            debug.Chat(0, "-kill");
            Expect.True(!unit.IsAlive, "-kill");
        }

        /// <summary>-timers lists the next timers; -leaks the scopes holding most and the counts; -scopes the tree (decisions 262 and 265).</summary>
        public static void Sections(FakeWorld world)
        {
            var debug = On(world);
            var holder = Game.Instance.Add(new LeakHolder());
            holder.After(5.0f, Nothing);
            holder.After(7.0f, Nothing);
            debug.Chat(0, "-timers");
            Expect.True(Shows(debug, "TIMERS"), "-timers shows");
            Expect.True(Shows(debug, "in 5.0 s · once"), "the timer due in 5 s");
            debug.Chat(0, "-leaks");
            Expect.True(!Shows(debug, "TIMERS"), "-leaks replaces -timers");
            Expect.True(Shows(debug, "LEAKS"), "-leaks shows");
            Expect.True(Shows(debug, "ended but held over a minute: 0"), "the held line");
            debug.Chat(0, "-scopes");
            Expect.True(Shows(debug, "SCOPES  of Game"), "-scopes shows the game's");
            Expect.True(Shows(debug, "  LeakHolder 1: 2 (Timer 2)"), "the holder's line: " + debug.PanelText);
            debug.Chat(0, "-scopes");
            Expect.True(!Shows(debug, "SCOPES"), "-scopes again hides it");
        }
    }
#endif
}
