using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check destructables: their classes, the watch on their deaths, and the sweep.</summary>
    public static class DestructableCases
    {
        /// <summary>Checks that the log is exactly the expected lines, in order.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(expected.Length, TestLog.Lines.Count, what + ": line count");
            for (var i = 0; i < expected.Length; i++)
            {
                Expect.Equal(expected[i], TestLog.Lines[i], what + ": line " + i);
            }
        }

        /// <summary>A destructable is watched for its death when its class or a component overrides OnDeath, and its death runs the type, then the components.</summary>
        public static void Watch(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gateHandle = world.NewDestructable(ObjectTypes.FourCC("B001"));
            var gate = Destructable.Of(gateHandle)!;
            Expect.True(gate is TestGate, "the destructable is a TestGate");
            Expect.Equal(1, world.Watched.Count, "the deaths asked to hear");
            Expect.Same(gateHandle, world.Watched[0], "the gate watched");

            var plainHandle = world.NewDestructable(ObjectTypes.FourCC("dfoo"));
            var plain = Destructable.Of(plainHandle)!;
            Expect.Equal(1, world.Watched.Count, "a plain destructable is not watched yet");
            plain.Add(new GatePart());
            Expect.Equal(2, world.Watched.Count, "the deaths asked to hear after a component");
            Expect.Same(plainHandle, world.Watched[1], "the plain destructable watched");

            TestLog.Lines.Clear();
            Inbound.DestructableDied(gateHandle);
            ExpectLog(new string[] { "gate died", "part died" }, "the log");
        }

        /// <summary>A destructable's X and Y are read once, when it is taken in, and kept after it is gone.</summary>
        public static void Position(FakeWorld world)
        {
            var handle = world.NewDestructable(ObjectTypes.FourCC("dfoo"));
            var found = Destructable.Of(handle)!;
            Expect.Near(100f, found.X, "X");
            Expect.Near(-50f, found.Y, "Y");
            found.Remove();
            Expect.Near(0f, world.DestructableX(handle), "the world no longer knows it");
            Expect.Near(100f, found.X, "X after Remove");
            Expect.Near(-50f, found.Y, "Y after Remove");
        }

        /// <summary>A destructable that vanishes is ended by the sweep, and the game is not asked to remove it.</summary>
        public static void SweepDetectsRemoval(FakeWorld world)
        {
            var handle = world.NewDestructable(ObjectTypes.FourCC("dfoo"));
            var found = Destructable.Of(handle)!;
            world.VanishDestructable(handle);
            world.Advance(0.3f);
            Expect.True(found.HasEnded, "the vanished destructable has ended");
            Expect.True(found.IsGone, "the vanished destructable is gone");
            Expect.Equal(0, world.RemovedDestructables.Count, "the destructables the game was asked to remove");
        }

        /// <summary>Makes plain destructables and takes them in.</summary>
        private static List<destructable> Known(FakeWorld world, int count)
        {
            var handles = new List<destructable>();
            for (var i = 0; i < count; i++)
            {
                var handle = world.NewDestructable(ObjectTypes.FourCC("dfoo"));
                Destructable.Of(handle);
                handles.Add(handle);
            }

            return handles;
        }

        /// <summary>A sweep reads at most Sweep.MostDestructables type ids, however many are known.</summary>
        public static void SweepCapped(FakeWorld world)
        {
            Known(world, Sweep.MostDestructables + 500);
            world.DestructableTypeReads = 0;
            world.Advance(0.26f);
            Expect.Equal(Sweep.MostDestructables, world.DestructableTypeReads, "the type ids one sweep read");
        }

        /// <summary>Past the cap the sweeps take turns: the last one known is read by the second sweep.</summary>
        public static void SweepTakesTurns(FakeWorld world)
        {
            var handles = Known(world, Sweep.MostDestructables + 500);
            var last = handles[handles.Count - 1];
            var found = Destructable.Of(last)!;
            world.VanishDestructable(last);
            world.Advance(0.26f);
            Expect.True(!found.HasEnded, "the first sweep did not reach it");
            world.Advance(0.25f);
            Expect.True(found.HasEnded, "the second sweep did");
            Expect.True(found.IsGone, "and it is gone");
        }

        /// <summary>Up to the cap every sweep reads them all, as it always did: the last one known ends at the first sweep.</summary>
        public static void SweepAtTheCap(FakeWorld world)
        {
            var handles = Known(world, Sweep.MostDestructables);
            var last = handles[handles.Count - 1];
            var found = Destructable.Of(last)!;
            world.VanishDestructable(last);
            world.Advance(0.26f);
            Expect.True(found.HasEnded, "the first sweep reached it");
        }

        /// <summary>Two gone side by side both end in one sweep, though each leaves the list as it ends.</summary>
        public static void SweepEndsNeighbours(FakeWorld world)
        {
            var handles = Known(world, 4);
            var first = Destructable.Of(handles[1])!;
            var second = Destructable.Of(handles[2])!;
            var after = Destructable.Of(handles[3])!;
            world.VanishDestructable(handles[1]);
            world.VanishDestructable(handles[2]);
            world.Advance(0.26f);
            Expect.True(first.HasEnded && second.HasEnded, "both ended");
            Expect.True(!after.HasEnded, "the one after them did not");
        }

        /// <summary>A reaction of a destructable the sweep ends may remove the others: the sweep stops at an empty list and nothing is reported.</summary>
        public static void SweepWhileTheListEmpties(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B002"), () => new TakingDownGate());
            var handle = world.NewDestructable(ObjectTypes.FourCC("B002"));
            var gate = (TakingDownGate)Destructable.Of(handle)!;
            var others = Known(world, 3);
            gate.Others.Add(Destructable.Of(others[0])!);
            gate.Others.Add(Destructable.Of(others[1])!);
            gate.Others.Add(Destructable.Of(others[2])!);
            world.VanishDestructable(handle);
            world.Advance(0.26f);
            Expect.True(gate.HasEnded, "the gate ended");
            Expect.Equal(0, KnownDestructables.Live.Count, "and the others with it");
            Expect.Equal(0, world.Logged.Count, "nothing was reported");
        }

        /// <summary>A destructable killed and then removed runs no death reaction from the removal: the game fires the death event again inside RemoveDestructable.</summary>
        public static void RemoveAfterDeath(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var handle = world.NewDestructable(ObjectTypes.FourCC("B001"));
            var gate = Destructable.Of(handle)!;
            TestLog.Lines.Clear();
            Inbound.DestructableDied(handle);
            gate.Remove();
            ExpectLog(new string[] { "gate died", "part died" }, "the log");
        }

        /// <summary>A living destructable that is removed reacts to no death, even when the game fires one inside RemoveDestructable.</summary>
        public static void RemoveLiving(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var handle = world.NewDestructable(ObjectTypes.FourCC("B001"));
            var gate = Destructable.Of(handle)!;
            TestLog.Lines.Clear();
            gate.Remove();
            ExpectLog(new string[0], "the log");
            Expect.True(gate.HasEnded, "the removed destructable has ended");
        }
    }

    /// <summary>A destructable that takes others down as it leaves.</summary>
    public class TakingDownGate : Destructable
    {
        /// <summary>The destructables it takes down.</summary>
        public readonly List<Destructable> Others = new List<Destructable>();

        /// <summary>Removes the others.</summary>
        protected override void OnLeave()
        {
            for (var i = 0; i < Others.Count; i++)
            {
                Others[i].Remove();
            }
        }
    }
}
