namespace Moonwell.Testing
{
    /// <summary>Cases for the unit pool (decision 220): a unit of a pooled type that ends is kept hidden and given out again by Unit.Create, as a new object.</summary>
    public static class PoolCases
    {
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A removed unit of a pooled type is put away, not removed, and the next Unit.Create of its type gives it out with its new owner, place and facing, its life as made, shown, as a new object.</summary>
        public static void KeptAndGivenOut(FakeWorld world)
        {
            ObjectTypes.Pooled(Footman);
            var first = Unit.Create(Footman, Player.Get(0), 10.0f, 20.0f, Game.Instance);
            var handle = first.Handle;
            var made = first.Life;
            first.Life = 1.0f;
            first.Remove();

            Expect.Equal(1, world.PutAwayUnits.Count, "put away");
            Expect.Equal(false, world.RemovedUnits.Contains(handle), "not removed");
            Expect.Equal(true, world.IsHidden(handle), "hidden");
            Expect.Equal(true, first.HasEnded, "the old object ended");
            Expect.Same(null, Unit.Of(handle), "no object while kept");

            var second = Unit.Create(Footman, Player.Get(1), 30.0f, 40.0f, Game.Instance, 90.0f);
            Expect.Same(handle, second.Handle, "the kept unit given out");
            Expect.Equal(false, first == second, "as a new object");
            Expect.Same(Player.Get(1), second.Owner, "its new owner");
            Expect.Equal(true, second.X == 30.0f && second.Y == 40.0f, "its new place");
            Expect.Equal(true, second.Life == made, "its life as made");
            Expect.Equal(false, world.IsHidden(handle), "shown");
            Expect.Same(second, Unit.Of(handle), "the handle's object");

            var third = Unit.Create(Footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            Expect.Equal(false, third.Handle == handle, "none kept: a new unit made");
        }

        /// <summary>A dead unit, a unit of a type not pooled, and a scope's unit when nothing is kept are removed as before.</summary>
        public static void NotKept(FakeWorld world)
        {
            ObjectTypes.Pooled(Footman);
            var dead = Unit.Create(Footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            dead.Kill();
            dead.Remove();
            var other = Unit.Create(ObjectTypes.FourCC("hkni"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            other.Remove();

            Expect.Equal(0, world.PutAwayUnits.Count, "nothing put away");
            Expect.Equal(true, world.RemovedUnits.Contains(dead.Handle), "the dead one removed");
            Expect.Equal(true, world.RemovedUnits.Contains(other.Handle), "the other type removed");
        }

        /// <summary>A unit of a pooled type that has morphed into a type not pooled is removed, not kept: given out again it would not be of the pooled type (decision 274).</summary>
        public static void MorphedNotKept(FakeWorld world)
        {
            ObjectTypes.Pooled(Footman);
            var unit = Unit.Create(Footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            world.MorphUnit(unit.Handle, ObjectTypes.FourCC("hkni"));
            unit.Remove();

            Expect.Equal(0, world.PutAwayUnits.Count, "nothing put away");
            Expect.Equal(true, world.RemovedUnits.Contains(unit.Handle), "the morphed one removed");
        }

        /// <summary>A unit kept when its scope ends: the pool keeps what a scope ends, not only Remove.</summary>
        public static void ScopeEnds(FakeWorld world)
        {
            ObjectTypes.Pooled(Footman);
            var wave = new TestScope("wave", new System.Collections.Generic.List<string>());
            Game.Instance.Adopt(wave);
            var unit = Unit.Create(Footman, Player.Get(0), 0.0f, 0.0f, wave);
            wave.End();

            Expect.Same(unit.Handle, world.PutAwayUnits[0], "kept when its scope ended");
        }
    }
}
