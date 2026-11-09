namespace Moonwell.Testing
{
    /// <summary>Cases for 0.10's live destructables and doodads: gates, looks and doodad animations (decisions 228, 231, 232).</summary>
    public static class DestructableLiveCases
    {
        private static TestGate MakeGate(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B0G1"), () => new TestGate());
            return (TestGate)Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("B0G1")))!;
        }

        /// <summary>Open kills a gate with its "death alternate" and runs no OnDeath; Close brings it back with its birth, then "stand", and runs no OnRestored.</summary>
        public static void GateOpenClose(FakeWorld world)
        {
            var gate = MakeGate(world);
            TestLog.Lines.Clear();

            gate.Open();
            Expect.True(gate.IsOpen, "open after Open");
            Expect.True(!gate.IsAlive, "an open gate is dead");
            Expect.Equal(0, TestLog.Lines.Count, "Open runs no OnDeath");
            Expect.Equal("animate death alternate", world.DestructableCalls[world.DestructableCalls.Count - 1], "Open's animation");

            var calls = world.DestructableCalls.Count;
            gate.Open();
            Expect.Equal(calls, world.DestructableCalls.Count, "Open on an open gate does nothing");

            gate.Close();
            Expect.True(!gate.IsOpen, "closed after Close");
            Expect.Near(gate.MaxLife, gate.Life, "a closed gate is at its most life");
            Expect.True(world.RestoredBirths[world.RestoredBirths.Count - 1], "Close shows the birth");
            Expect.Equal("animate stand", world.DestructableCalls[world.DestructableCalls.Count - 1], "Close's animation");
            Expect.Equal(0, TestLog.Lines.Count, "Close runs no OnRestored");

            gate.Kill();
            Expect.Equal("gate died", TestLog.Lines[0], "a kill still runs OnDeath");
            Expect.True(!gate.IsOpen, "a killed gate is not open");
        }

        /// <summary>A destroyed gate opened shows as opened; Restore of an open gate closes it and runs OnRestored.</summary>
        public static void GateRestore(FakeWorld world)
        {
            var gate = MakeGate(world);
            gate.Kill();
            gate.Open();
            Expect.True(gate.IsOpen, "a destroyed gate opened is open");

            TestLog.Lines.Clear();
            gate.Restore();
            Expect.True(!gate.IsOpen, "Restore closes it");
            Expect.Equal("gate restored", TestLog.Lines[0], "Restore runs OnRestored");
        }

        /// <summary>Show, Hide, animations, tint, invulnerability, occluder height and name reach the game, and do nothing once the destructable is gone.</summary>
        public static void Looks(FakeWorld world)
        {
            var tree = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTlt")))!;
            Expect.True(tree.IsShown, "shown at first");
            tree.Hide();
            Expect.True(!tree.IsShown, "hidden after Hide");
            tree.Show();
            Expect.True(tree.IsShown, "shown after Show");
            tree.PlayAnimation("stand hit");
            tree.QueueAnimation("stand");
            Expect.Near(1f, tree.AnimationSpeed, "speed 1 until set");
            tree.AnimationSpeed = 0.5f;
            Expect.Near(0.5f, tree.AnimationSpeed, "the speed set");
            tree.Tint(300, -5, 10);
            tree.Invulnerable = true;
            Expect.True(tree.Invulnerable, "invulnerable after set");
            tree.OccluderHeight = 120f;
            Expect.Near(120f, tree.OccluderHeight, "the occluder height set");
            string name = "";
            Local.For(Player.One, () => name = tree.Name);
            Expect.Equal("Summer Tree Wall", name, "the name");

            var expected = new string[] { "show False", "show True", "animate stand hit", "queue stand", "speed 0.5", "tint 255 0 10 255", "invulnerable True", "occluder 120" };
            Expect.Equal(expected.Length, world.DestructableCalls.Count, "the calls");
            for (var i = 0; i < expected.Length; i++)
            {
                Expect.Equal(expected[i], world.DestructableCalls[i], "call " + i);
            }

            tree.Remove();
            tree.Hide();
            tree.PlayAnimation("death");
            tree.Tint(0, 0, 0);
            tree.Invulnerable = false;
            Expect.Equal(expected.Length, world.DestructableCalls.Count, "nothing reaches the game once gone");
            Expect.True(!tree.Invulnerable, "not invulnerable once gone");
            Expect.Near(0f, tree.OccluderHeight, "no occluder height once gone");
        }

        /// <summary>Doodad animations near a place and inside a rect reach the game with their type.</summary>
        public static void DoodadAnimations(FakeWorld world)
        {
            var torch = ObjectTypes.FourCC("LOtr");
            Doodad.PlayAnimation(torch, 10f, 20f, 300f, "death");
            Doodad.PlayAnimationIn(torch, -100f, -200f, 100f, 200f, "birth");
            Expect.Equal("near " + torch + " 10 20 300 False death", world.DoodadAnimations[0], "near a place");
            Expect.Equal("in " + torch + " -100 -200 100 200 birth", world.DoodadAnimations[1], "inside a rect");
        }
    }
}
