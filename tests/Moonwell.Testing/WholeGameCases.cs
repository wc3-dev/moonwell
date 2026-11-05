using Moonwell.Engine;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A destructable that removes another destructable as it leaves.</summary>
    public sealed class Gate : Destructable
    {
        /// <summary>The destructable it takes with it.</summary>
        public static Destructable? Post;

        /// <summary>Removes the post.</summary>
        protected override void OnLeave()
        {
            Post?.Remove();
        }
    }

    /// <summary>A destructable that watches its death.</summary>
    public sealed class Barricade : Destructable
    {
        /// <summary>Does nothing; it makes the world deliver the destructable's death.</summary>
        public override void OnDeath()
        {
        }
    }

    /// <summary>A unit component whose removal looks for units near its unit, the first ask for units, and logs how many it finds.</summary>
    public sealed class LooksAround : UnitComponent
    {
        /// <summary>Logs the units found within 500 of the unit.</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("found " + Find.InRange<Unit>(Unit.X, Unit.Y, 500.0f).Count);
        }
    }

    /// <summary>A player component with no reactions.</summary>
    public sealed class Roster : PlayerComponent
    {
    }

    /// <summary>Cases for what lasts the whole game, the live lists and the scheduler: the game and the players are never ended, loops over live lists miss none, and ended timers leave the scheduler.</summary>
    public static class WholeGameCases
    {
        /// <summary>Ending the game is refused and reported, in every build: Moonwell's own timers and the map's later ones still run.</summary>
        public static void GameEndRefused(FakeWorld world)
        {
            var before = world.Logged.Count;
            Game.Instance.End();
            Expect.Equal(before + 1, world.Logged.Count, "the end reported");
            Expect.True(!Game.Instance.HasEnded, "the game has not ended");
            var ran = false;
            Game.Instance.After(1.0f, () => ran = true);
            world.Advance(1.1f);
            Expect.True(ran, "a timer on the game ran");
        }

        /// <summary>Ending a player is refused and reported, in every build: its components stay.</summary>
        public static void PlayerEndRefused(FakeWorld world)
        {
            var player = Player.Get(0);
            var watch = player.Add(new Roster());
            var before = world.Logged.Count;
            player.End();
            Expect.Equal(before + 1, world.Logged.Count, "the end reported");
            Expect.True(!player.HasEnded && !watch.HasEnded, "the player and its component have not ended");
        }

        /// <summary>The spell-buff sweep checks every unit, though a buff's OnApply removes a unit before the one it is on.</summary>
        public static void SweepAfterRemoval(FakeWorld world)
        {
            SpellBuffCases.Watch();
            var raw = new List<unit>();
            for (var i = 0; i < 3; i++)
            {
                raw.Add(world.NewUnit(ObjectTypes.FourCC("hfoo")));
                Inbound.Entered(raw[i]);
                world.GameBuffs.Give(raw[i], SpellBuffCases.Frozen);
            }

            var first = Unit.Of(raw[0])!;
            var second = Unit.Of(raw[1])!;
            FrozenBuff.WhenApplied = buff =>
            {
                if (buff.Unit == second)
                {
                    first.Remove();
                }
            };
            world.Advance(1.1f);
            FrozenBuff.WhenApplied = null;
            Expect.True(Unit.Of(raw[2])!.Has<FrozenBuff>(), "the third unit's buff found in the same sweep");
        }

        /// <summary>A destructable the game removed, whose OnLeave removes one before it in the list, does not make the sweep miss the one after it.</summary>
        public static void DestructableSweepAfterRemoval(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B0gt"), () => new Gate());
            var post = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTbr")))!;
            var gate = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("B0gt")))!;
            var after = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTbr")))!;

            // More than a run checks, so the run does not go round to the first again.
            for (var i = 0; i < Sweep.MostDestructables; i++)
            {
                Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTbr")));
            }

            Gate.Post = post;
            world.VanishDestructable(gate.Handle);
            world.VanishDestructable(after.Handle);
            Sweep.Run();
            Gate.Post = null;
            Expect.True(gate.HasEnded && post.HasEnded, "the gate and its post ended");
            Expect.True(after.HasEnded, "the one after the gate ended in the same sweep");
        }

        /// <summary>Timers ended long before they are due leave the scheduler soon, not at their due time.</summary>
        public static void EndedTimersLeave(FakeWorld world)
        {
            var held = Runtime.Scheduler.Count;
            var timers = new List<Timer>();
            for (var i = 0; i < 300; i++)
            {
                timers.Add(Game.Instance.After(60.0f, () => { }));
            }

            for (var i = 0; i < 299; i++)
            {
                timers[i].End();
            }

            var ran = false;
            Game.Instance.After(1.0f, () => ran = true);
            Expect.Equal(held + 2, Runtime.Scheduler.Count, "the timers the scheduler holds, Moonwell's own with them");
            world.Advance(1.1f);
            Expect.True(ran, "the new timer ran");
            world.Advance(60.0f);
            Expect.True(timers[299].HasEnded, "the timer left on ran and ended");
        }

        /// <summary>A unit that is ending is not tracked when units are first asked for in its removal, and not found.</summary>
        public static void EndingUnitNotTracked(FakeWorld world)
        {
            Interactions.Install(new FakeInteractionWorld());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
            unit.Add(new LooksAround());
            TestLog.Lines.Clear();
            unit.Remove();
            Expect.Equal("found 0", string.Join(", ", TestLog.Lines), "the log");
            Expect.True(Tracking.EntryOf(unit) == null, "the removed unit is not tracked");
        }

        /// <summary>The ending listeners run in a fixed order, the interactions package's before the areas package's, whichever was added first.</summary>
        public static void EndingListenersOrdered(FakeWorld world)
        {
            var order = new List<string>();
            Hooks.AddEnding(widget => order.Add("areas"), Hooks.AreasEnding);
            Hooks.AddEnding(widget => order.Add("interactions"), Hooks.InteractionsEnding);
            Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!.Remove();
            Expect.Equal("interactions, areas", string.Join(", ", order), "the order");
        }

        /// <summary>A destructable whose death is watched is no longer watched once it has left.</summary>
        public static void DeathWatchEnds(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B0bc"), () => new Barricade());
            var barricade = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("B0bc")))!;
            Expect.True(world.Watched.Contains(barricade.Handle), "watched while in the game");
            barricade.Remove();
            Expect.True(!world.Watched.Contains(barricade.Handle), "not watched once removed");
        }

        /// <summary>A set's order holds through many removals and adds: an item removed and added again comes last.</summary>
        public static void SetOrderAfterRemovals(FakeWorld world)
        {
            var set = new LiveSet<int>();
            for (var i = 0; i < 10; i++)
            {
                set.Add(i);
            }

            set.Remove(0);
            set.Remove(5);
            set.Remove(9);
            Expect.True(!set.Contains(5), "a removed one is not there");
            set.Add(5);
            Expect.Equal(8, set.Count, "the count");
            var seen = new List<string>();
            for (var i = 0; i < set.Count; i++)
            {
                seen.Add(set[i].ToString());
            }

            Expect.Equal("1 2 3 4 6 7 8 5", string.Join(" ", seen), "the order by index");
            Expect.Equal(8, set.Snapshot().Count, "the snapshot");
        }
    }
}
