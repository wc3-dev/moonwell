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

        /// <summary>Create makes a destructable of a type through the game, facing 270 at scale 1 and variation 0 unless given, takes it in where the game put it, and puts it in the scope, which removes it when it ends.</summary>
        public static void CreateInScope(FakeWorld world)
        {
            var scope = new TestScope("scope", new List<string>());
            var tree = Destructable.Create(ObjectTypes.FourCC("LTlt"), 1000f, 1000f, scope);
            var asked = world.DestructableSpawns[0];
            Expect.Equal(ObjectTypes.FourCC("LTlt"), asked.TypeId, "the type the game was asked for");
            Expect.Near(1000f, asked.X, "the x asked");
            Expect.Near(1000f, asked.Y, "the y asked");
            Expect.Near(270f, asked.Facing, "facing 270");
            Expect.Near(1f, asked.Scale, "scale 1");
            Expect.Equal(0, asked.Variation, "variation 0");
            Expect.Equal(ObjectTypes.FourCC("LTlt"), tree.TypeId, "its type");
            Expect.Near(960f, tree.X, "X, where the game put it");
            Expect.Near(960f, tree.Y, "Y, where the game put it");
            Expect.Same(scope, tree.Parent, "its scope");
            var turned = Destructable.Create(ObjectTypes.FourCC("LTlt"), 0f, 0f, scope, 90f, 1.5f, 3);
            var given = world.DestructableSpawns[1];
            Expect.Near(90f, given.Facing, "the facing given");
            Expect.Near(1.5f, given.Scale, "the scale given");
            Expect.Equal(3, given.Variation, "the variation given");
            scope.End();
            Expect.True(tree.HasEnded && turned.HasEnded, "both ended with the scope");
            Expect.True(world.RemovedDestructables.Contains(tree.Handle) && world.RemovedDestructables.Contains(turned.Handle), "and the game removed both");
        }

        /// <summary>Create of a registered class makes it with its class's id, as an object of the class with its starting components.</summary>
        public static void CreateClass(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            Expect.Equal(ObjectTypes.FourCC("B001"), gate.TypeId, "its type");
            Expect.Equal(1, gate.GetAll<GatePart>().Count, "its starting part");
            Expect.True(world.Watched.Contains(gate.Handle), "its death is watched");
        }

        /// <summary>Create of a type the game makes nothing of throws, naming the id.</summary>
        public static void CreateUnmakeable(FakeWorld world)
        {
            world.Unmakeable.Add(ObjectTypes.FourCC("hfoo"));
            Expect.Throws<MoonwellException>(() => Destructable.Create(ObjectTypes.FourCC("hfoo"), 0f, 0f, Game.Instance), "a unit's id");
        }

        /// <summary>A destructable made in a scope that has ended ends at once and is removed, and runs no OnCreated.</summary>
        public static void CreateInEndedScope(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B003"), () => new CreatedGate());
            var scope = new TestScope("ended", new List<string>());
            scope.End();
            TestLog.Lines.Clear();
            var gate = Destructable.Create<CreatedGate>(0f, 0f, scope);
            Expect.True(gate.HasEnded, "it ended at once");
            Expect.True(world.RemovedDestructables.Contains(gate.Handle), "and the game removed it");
            ExpectLog(new string[0], "no OnCreated");
        }

        /// <summary>Kill kills the destructable through the game, which runs OnDeath and its components' inside the call; it reads life 0 and is not alive after.</summary>
        public static void KillRunsDeath(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            Expect.True(gate.IsAlive, "alive when made");
            TestLog.Lines.Clear();
            gate.Kill();
            ExpectLog(new string[] { "gate died", "part died" }, "the deaths, inside the call");
            Expect.Near(0f, gate.Life, "its life");
            Expect.True(!gate.IsAlive, "not alive");
            Expect.True(!gate.HasEnded, "a dead destructable stays in the game");
        }

        /// <summary>Kill on a dead destructable does nothing, as the game fires no second death; nor on one that is gone.</summary>
        public static void KillDeadOrGone(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            gate.Kill();
            TestLog.Lines.Clear();
            gate.Kill();
            ExpectLog(new string[0], "a second kill");
            var other = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            other.Remove();
            TestLog.Lines.Clear();
            world.CallsThrow = true;
            other.Kill();
            world.CallsThrow = false;
            ExpectLog(new string[0], "a kill once gone");
            Expect.True(!other.IsAlive, "one that is gone is not alive");
        }

        /// <summary>Life reads the game's; setting it keeps a fraction and stays at most the most life.</summary>
        public static void LifeSets(FakeWorld world)
        {
            var tree = Destructable.Create(ObjectTypes.FourCC("LTlt"), 0f, 0f, Game.Instance);
            Expect.Near(50f, tree.Life, "a new tree's life");
            tree.Life = 20.5f;
            Expect.Near(20.5f, tree.Life, "set to 20.5");
            tree.Life = 999f;
            Expect.Near(50f, tree.Life, "set above the most");
        }

        /// <summary>Life set to 0.4 or less kills the destructable, OnDeath inside the call; 0.405 does not (seen 11-02).</summary>
        public static void LifeLowKills(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var living = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            TestLog.Lines.Clear();
            living.Life = 0.405f;
            ExpectLog(new string[0], "0.405 kills nothing");
            Expect.True(living.IsAlive, "alive at 0.405");
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            gate.Life = 0.4f;
            ExpectLog(new string[] { "gate died", "part died" }, "0.4 kills, inside the call");
            Expect.Near(0f, gate.Life, "its life");
        }

        /// <summary>Life set on a dead destructable does nothing, as the game would leave a stump that reads the life: Restore brings one back.</summary>
        public static void LifeOnDead(FakeWorld world)
        {
            var tree = Destructable.Create(ObjectTypes.FourCC("LTlt"), 0f, 0f, Game.Instance);
            tree.Kill();
            tree.Life = 20f;
            Expect.Near(0f, tree.Life, "its life");
            Expect.True(!tree.IsAlive, "still dead");
        }

        /// <summary>Once the game has removed a destructable, Life and MaxLife read the last values read, setting them does nothing, and the game is not asked.</summary>
        public static void LifeGone(FakeWorld world)
        {
            var tree = Destructable.Create(ObjectTypes.FourCC("LTlt"), 0f, 0f, Game.Instance);
            tree.Life = 20f;
            Expect.Near(50f, tree.MaxLife, "its most life");
            world.VanishDestructable(tree.Handle);
            Expect.Near(20f, tree.Life, "the last life");
            Expect.Near(50f, tree.MaxLife, "the last most life");
            Expect.True(!tree.IsAlive, "not alive");
            world.Advance(0.3f);
            world.CallsThrow = true;
            tree.Life = 30f;
            tree.MaxLife = 70f;
            var life = tree.Life;
            var most = tree.MaxLife;
            tree.Kill();
            tree.Restore();
            world.CallsThrow = false;
            Expect.Near(20f, life, "the last life, once swept");
            Expect.Near(50f, most, "the last most life, once swept");
        }

        /// <summary>MaxLife is a whole number, as the game keeps it; a full destructable stays full, and one above a lower most falls to it.</summary>
        public static void MaxLifeSets(FakeWorld world)
        {
            var tree = Destructable.Create(ObjectTypes.FourCC("LTlt"), 0f, 0f, Game.Instance);
            tree.MaxLife = 80.7f;
            Expect.Near(81f, tree.MaxLife, "80.7 is 81");
            Expect.Near(81f, tree.Life, "full stays full");
            tree.MaxLife = 10f;
            Expect.Near(10f, tree.Life, "falls to a lower most");
        }

        /// <summary>A destructable with no handle calls no native for its most life: a set goes to Declare and a read to DeclaredValue, which reads the value of the type its class is registered with.</summary>
        public static void MaxLifeWithoutHandle(FakeWorld world)
        {
            var id = ObjectTypes.FourCC("B0rc");
            ObjectTypes.Destructable(id, () => new RecordingGate());
            world.SetTypeValue(id, "bhps", 300f);
            var gate = new RecordingGate();
            world.CallsThrow = true;
            gate.MaxLife = 500f;
            var most = gate.MaxLife;
            world.CallsThrow = false;
            Expect.Near(300f, most, "the most life its type declares");
            Expect.Equal(2, gate.Asked.Count, "hook calls");
            Expect.Equal("declare bhps 500", gate.Asked[0], "the set");
            Expect.Equal("read bhps", gate.Asked[1], "the read");
        }

        /// <summary>Restore brings a dead destructable back at its most life, without the birth animation unless asked, and runs OnRestored and its components' inside the call.</summary>
        public static void RestoreRunsRestored(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            gate.MaxLife = 80f;
            gate.Kill();
            TestLog.Lines.Clear();
            gate.Restore();
            ExpectLog(new string[] { "gate restored", "part restored" }, "the reactions, inside the call");
            Expect.Near(80f, world.RestoredLives[0], "the life the game was given");
            Expect.True(!world.RestoredBirths[0], "without the birth animation");
            Expect.Near(80f, gate.Life, "its most life");
            Expect.True(gate.IsAlive, "alive");
            gate.Kill();
            gate.Restore(true);
            Expect.True(world.RestoredBirths[1], "with the birth animation");
        }

        /// <summary>A destructable restored and killed again dies again.</summary>
        public static void RestoreThenKill(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            gate.Kill();
            gate.Restore();
            TestLog.Lines.Clear();
            gate.Kill();
            ExpectLog(new string[] { "gate died", "part died" }, "the second death");
        }

        /// <summary>Restore on a living destructable does nothing, as the game does nothing, and runs no OnRestored.</summary>
        public static void RestoreLiving(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var gate = Destructable.Create<TestGate>(0f, 0f, Game.Instance);
            gate.Life = 20f;
            TestLog.Lines.Clear();
            gate.Restore();
            ExpectLog(new string[0], "no reaction");
            Expect.Equal(0, world.RestoredLives.Count, "the game was not asked");
            Expect.Near(20f, gate.Life, "its life");
        }

        /// <summary>A component that overrides only OnRestored hears a restore, and a reaction that throws is reported while the rest still run.</summary>
        public static void RestoredReportsErrors(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B004"), () => new ThrowingGate());
            var gate = Destructable.Create<ThrowingGate>(0f, 0f, Game.Instance);
            gate.Add(new RestoredPart());
            gate.Kill();
            TestLog.Lines.Clear();
            gate.Restore();
            ExpectLog(new string[] { "restored part" }, "the component still ran");
            Expect.Equal(1, world.Logged.Count, "one error");
            Expect.Equal("Moonwell: ThrowingGate.OnRestored failed: boom", world.Logged[0], "the logged line");
        }
    }

    /// <summary>A destructable that logs its OnCreated.</summary>
    public class CreatedGate : Destructable
    {
        /// <summary>Logs that it came in.</summary>
        protected override void OnCreated()
        {
            TestLog.Lines.Add("created");
        }
    }

    /// <summary>A destructable whose OnRestored throws.</summary>
    public class ThrowingGate : Destructable
    {
        /// <summary>Throws.</summary>
        public override void OnRestored()
        {
            throw new System.InvalidOperationException("boom");
        }
    }

    /// <summary>A component that logs only a restore.</summary>
    public class RestoredPart : DestructableComponent
    {
        /// <summary>Logs the restore.</summary>
        public override void OnRestored()
        {
            TestLog.Lines.Add("restored part");
        }
    }

    /// <summary>A destructable that records its declared-value hooks.</summary>
    public class RecordingGate : Destructable
    {
        /// <summary>The hook calls, in turn, as "read" or "declare", the field id, and the value declared.</summary>
        public List<string> Asked = new List<string>();

        /// <summary>Records the read, and reads the value as Destructable does.</summary>
        protected override T DeclaredValue<T>(string field)
        {
            Asked.Add("read " + field);
            return base.DeclaredValue<T>(field);
        }

        /// <summary>Records the declared value.</summary>
        protected override void Declare(string field, object value)
        {
            Asked.Add("declare " + field + " " + value);
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
