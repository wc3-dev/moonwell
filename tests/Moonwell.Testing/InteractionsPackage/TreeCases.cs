namespace Moonwell.Testing
{
    /// <summary>Cases for Regrow, on trees.</summary>
    public static class TreeCases
    {
        /// <summary>A tree with Regrow comes back with its birth when the seconds are up after its death, and not before; a gate's Open is no death.</summary>
        public static void Regrows(FakeWorld world)
        {
            var tree = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTlt")))!;
            tree.Add(new Regrow(5f));
            tree.Kill();
            world.Advance(4.9f);
            Expect.True(!tree.IsAlive, "still dead before the seconds are up");
            world.Advance(0.2f);
            Expect.True(tree.IsAlive, "back when they are up");
            Expect.True(world.RestoredBirths[world.RestoredBirths.Count - 1], "with its birth");

            tree.Open();
            world.Advance(6f);
            Expect.True(!tree.IsAlive, "an opened one does not come back");
        }

        /// <summary>A Restore before the seconds are up stops the wait, and a later death starts a new one.</summary>
        public static void RegrowRestoredEarly(FakeWorld world)
        {
            var tree = Destructable.Of(world.NewDestructable(ObjectTypes.FourCC("LTlt")))!;
            tree.Add(new Regrow(5f, showBirth: false));
            tree.Kill();
            world.Advance(1f);
            tree.Restore();
            world.Advance(1f);
            tree.Kill();
            world.Advance(3.5f);
            Expect.True(!tree.IsAlive, "the first wait was stopped");
            world.Advance(1.6f);
            Expect.True(tree.IsAlive, "back 5 seconds after the second death");
            Expect.True(!world.RestoredBirths[world.RestoredBirths.Count - 1], "without its birth");
        }
    }
}
