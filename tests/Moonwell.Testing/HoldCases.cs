using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A unit component that logs its adding and removal, and can run an action of a case's in OnAdded.</summary>
    public sealed class Kept : UnitComponent
    {
        /// <summary>What OnAdded runs after it logs; nothing unless a case sets it.</summary>
        public Action? WhenAdded;

        /// <summary>Logs "kept added", then runs WhenAdded.</summary>
        protected override void OnAdded()
        {
            TestLog.Lines.Add("kept added");
            WhenAdded?.Invoke();
        }

        /// <summary>Logs "kept removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("kept removed");
        }
    }

    /// <summary>Cases for holders: a component added with heldBy ends when its holder ends.</summary>
    public static class HoldCases
    {
        /// <summary>A footman to add components to.</summary>
        private static Unit NewUnit(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        /// <summary>How many lines of the log equal a line.</summary>
        private static int CountOf(string line)
        {
            var count = 0;
            for (var i = 0; i < TestLog.Lines.Count; i++)
            {
                if (TestLog.Lines[i] == line)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>A component held by a scope ends when the scope ends, with OnRemoved run once.</summary>
        public static void EndsWithHolder(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            TestLog.Lines.Clear();
            var kept = unit.Add(new Kept(), heldBy: holder);
            Expect.True(unit.Has<Kept>(), "the unit has it while held");
            Expect.Equal(1, holder.ChildCount, "the holder holds one thing");
            holder.End();
            Expect.True(kept.HasEnded, "it ended with its holder");
            Expect.True(!unit.Has<Kept>(), "the unit no longer has it");
            Expect.Equal(1, CountOf("kept removed"), "OnRemoved ran once");
        }

        /// <summary>Two components of one class with two holders are two components, each ending with its own holder (decision 17).</summary>
        public static void TwoComponents(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            var a = unit.Add(new Kept(), heldBy: first);
            var b = unit.Add(new Kept(), heldBy: second);
            Expect.Equal(2, unit.GetAll<Kept>().Count, "two components");
            first.End();
            Expect.True(a.HasEnded, "the first ended with its holder");
            Expect.True(!b.HasEnded, "the second stays");
            Expect.Equal(1, unit.GetAll<Kept>().Count, "one left");
        }

        /// <summary>A component added with a holder that has ended is not added, and comes back ended.</summary>
        public static void EndedHolder(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            holder.End();
            TestLog.Lines.Clear();
            var kept = unit.Add(new Kept(), heldBy: holder);
            Expect.Equal(0, unit.ComponentCount, "nothing was added");
            Expect.True(kept.HasEnded, "the component has ended");
            Expect.Equal(0, CountOf("kept added"), "OnAdded never ran");
        }

        /// <summary>A component removed by code lets go of its holders: their child counts drop, and a holder ending afterwards changes nothing.</summary>
        public static void RemovedReleases(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            var kept = unit.Add(new Kept(), heldBy: first);
            unit.Add(new Kept(), heldBy: second);
            TestLog.Lines.Clear();
            unit.Remove(kept);
            Expect.Equal(0, first.ChildCount, "the first holder holds nothing");
            Expect.Equal(1, second.ChildCount, "the second still holds its own");
            first.End();
            Expect.Equal(1, CountOf("kept removed"), "only the removal ran OnRemoved");
            Expect.Equal(1, unit.GetAll<Kept>().Count, "the second component stays");
        }

        /// <summary>A holder that ends inside the held component's own OnAdded ends it once, as End() from inside does.</summary>
        public static void EndInsideReaction(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            TestLog.Lines.Clear();
            var kept = new Kept();
            kept.WhenAdded = () => holder.End();
            unit.Add(kept, heldBy: holder);
            Expect.True(kept.HasEnded, "it ended with its holder");
            Expect.Equal(1, CountOf("kept removed"), "OnRemoved ran once");
            Expect.Equal(0, holder.ChildCount, "the holder holds nothing");
            Expect.Equal(0, unit.ComponentCount, "the unit has none");
        }

        /// <summary>Two applies of a refreshed buff from two holders share one buff, which ends only when the second holder ends.</summary>
        public static void HeldShared(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            var buff = unit.Apply(new Lasting(), heldBy: first);
            var again = unit.Apply(new Lasting(), heldBy: second);
            Expect.Same(buff, again, "the second apply refreshed the first buff");
            first.End();
            Expect.True(!buff.HasEnded, "it stays while the second holder holds it");
            second.End();
            Expect.True(buff.HasEnded, "it ended with its last holder");
        }

        /// <summary>A held buff with a duration ends when the duration runs out, its holder still there, and lets go of the holder.</summary>
        public static void HeldDurationFirst(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            var buff = unit.Apply(new Burn(), heldBy: holder);
            world.Advance(3.1f);
            Expect.True(buff.HasEnded, "it ended at its duration");
            Expect.True(!holder.HasEnded, "the holder is still there");
            Expect.Equal(0, holder.ChildCount, "the holder holds nothing");
        }

        /// <summary>A held buff applied again with no holder stays when its holder ends, and lasts its duration.</summary>
        public static void HeldThenUnheld(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            var buff = unit.Apply(new Burn(), heldBy: holder);
            unit.Apply(new Burn());
            holder.End();
            Expect.True(!buff.HasEnded, "it stays once applied with no holder");
            world.Advance(3.1f);
            Expect.True(buff.HasEnded, "it ended at its duration");
        }

        /// <summary>Separate buffs with two holders are two buffs, each ending with its own holder.</summary>
        public static void HeldSeparate(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            unit.Apply(new SeparateBurn(), heldBy: first);
            unit.Apply(new SeparateBurn(), heldBy: second);
            Expect.Equal(2, unit.GetAll<SeparateBurn>().Count, "two buffs");
            first.End();
            Expect.Equal(1, unit.GetAll<SeparateBurn>().Count, "one left after the first holder ended");
        }

        /// <summary>Removing a held buff ends it whoever holds it, and both holders let go.</summary>
        public static void HeldRemoved(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            var buff = unit.Apply(new Lasting(), heldBy: first);
            unit.Apply(new Lasting(), heldBy: second);
            Expect.True(unit.Remove<Lasting>(), "there was one to remove");
            Expect.True(buff.HasEnded, "it ended");
            Expect.Equal(0, first.ChildCount, "the first holder holds nothing");
            Expect.Equal(0, second.ChildCount, "the second holder holds nothing");
        }

        /// <summary>Checks that the log is exactly the expected lines, in order.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(expected.Length, TestLog.Lines.Count, what + ": line count");
            for (var i = 0; i < expected.Length && i < TestLog.Lines.Count; i++)
            {
                Expect.Equal(expected[i], TestLog.Lines[i], what + ": line " + i);
            }
        }

        /// <summary>A strongest buff takes a stronger apply at once: one buff, OnApply run again, and the stronger one's strength and source.</summary>
        public static void StrongestUp(FakeWorld world)
        {
            var unit = NewUnit(world);
            var weakSource = NewUnit(world);
            var strongSource = NewUnit(world);
            TestLog.Lines.Clear();
            var buff = unit.Apply(new Strong(1f), weakSource, new TestScope("weak", new List<string>()));
            var again = unit.Apply(new Strong(3f), strongSource, new TestScope("strong", new List<string>()));
            Expect.Same(buff, again, "one buff");
            ExpectLog(new string[] { "apply 1", "apply 3" }, "the log");
            Expect.True(buff.Strength == 3f, "the strength reads 3");
            Expect.Same(strongSource, buff.Source, "the source is the stronger one's");
        }

        /// <summary>When the stronger holder ends, the weaker one's strength takes over with no gap.</summary>
        public static void StrongestDown(FakeWorld world)
        {
            var unit = NewUnit(world);
            var weakSource = NewUnit(world);
            var strong = new TestScope("strong", new List<string>());
            var buff = unit.Apply(new Strong(1f), weakSource, new TestScope("weak", new List<string>()));
            unit.Apply(new Strong(3f), NewUnit(world), strong);
            TestLog.Lines.Clear();
            strong.End();
            ExpectLog(new string[] { "apply 1" }, "the log after the stronger left");
            Expect.True(buff.Strength == 1f, "the strength reads 1");
            Expect.True(!buff.HasEnded, "no gap: the buff is still on");
            Expect.Same(weakSource, buff.Source, "the source is the weaker one's");
        }

        /// <summary>An apply as strong as the one running changes nothing: no OnApply.</summary>
        public static void StrongestTie(FakeWorld world)
        {
            var unit = NewUnit(world);
            var firstSource = NewUnit(world);
            var buff = unit.Apply(new Strong(1f), firstSource, new TestScope("first", new List<string>()));
            TestLog.Lines.Clear();
            unit.Apply(new Strong(1f), NewUnit(world), new TestScope("second", new List<string>()));
            Expect.Equal(0, TestLog.Lines.Count, "no OnApply for a tie");
            Expect.Same(firstSource, buff.Source, "the running one's source stays");
        }

        /// <summary>A strongest buff ends when its last holder ends, with OnRemoved once.</summary>
        public static void StrongestLast(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = new TestScope("first", new List<string>());
            var second = new TestScope("second", new List<string>());
            var buff = unit.Apply(new Strong(1f), heldBy: first);
            unit.Apply(new Strong(3f), heldBy: second);
            TestLog.Lines.Clear();
            second.End();
            first.End();
            Expect.True(buff.HasEnded, "it ended with its last holder");
            ExpectLog(new string[] { "apply 1", "strong removed" }, "the log");
        }

        /// <summary>Timed applies: the stronger runs until its own end, then the weaker until its end, which ends the buff; Remaining reads the last end.</summary>
        public static void StrongestTimed(FakeWorld world)
        {
            var unit = NewUnit(world);
            TestLog.Lines.Clear();
            var buff = unit.Apply(new Strong(3f, 2f));
            unit.Apply(new Strong(1f, 5f));
            Expect.True(buff.Strength == 3f, "the strength reads 3 at first");
            world.Advance(1.0f);
            Expect.True(buff.Remaining > 3.9f && buff.Remaining < 4.1f, "Remaining reads the last end: " + buff.Remaining);
            world.Advance(1.1f);
            Expect.True(buff.Strength == 1f, "the strength reads 1 after the stronger ran out");
            Expect.True(!buff.HasEnded, "no gap");
            world.Advance(3.0f);
            Expect.True(buff.HasEnded, "it ended at the last apply's end");
            ExpectLog(new string[] { "apply 3", "apply 1", "strong expire", "strong removed" }, "the log");
        }

        /// <summary>A weaker apply that comes just before the strongest one's holder ends, in one instant, takes over with no gap and one OnApply.</summary>
        public static void StrongestHandover(FakeWorld world)
        {
            var unit = NewUnit(world);
            var strong = new TestScope("strong", new List<string>());
            var buff = unit.Apply(new Strong(3f), heldBy: strong);
            TestLog.Lines.Clear();
            Game.Instance.After(0.5f, () =>
            {
                unit.Apply(new Strong(2f), heldBy: new TestScope("next", new List<string>()));
                strong.End();
            });
            world.Advance(0.6f);
            Expect.True(!buff.HasEnded, "no gap");
            ExpectLog(new string[] { "apply 2" }, "the log");
        }

#if DEBUG
        /// <summary>A component added with a holder that has ended is reported in a development build.</summary>
        public static void EndedHolderReported(FakeWorld world)
        {
            var unit = NewUnit(world);
            var holder = new TestScope("holder", new List<string>());
            holder.End();
            var before = world.Printed.Count;
            unit.Add(new Kept(), heldBy: holder);
            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("has ended"), "the report says why: " + world.Printed[before]);
        }
#endif
    }
}
