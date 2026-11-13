namespace Moonwell.Testing
{
    /// <summary>Cases for a unit brain's orders (decision 300): a repeat reaches the game only when the unit no longer carries it out or has not done it.</summary>
    public static class BrainOrderCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>The game's id of "move" in the fake.</summary>
        private const int MoveId = 851986;

        /// <summary>A point order: a repeat is dropped while carried and once done near the place, and sent after another order, idle away from the place, after an owner change, or beyond 32.</summary>
        public static void PointOrders(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            world.Orders.Clear();

            Expect.True(brain.OrderAt("move", 500f, 0f), "the first is sent");
            Expect.Equal(1, world.Orders.Count, "one order");
            Expect.True(brain.OrderAt("move", 520f, 20f), "a repeat within 32 gives true");
            Expect.Equal(1, world.Orders.Count, "and is dropped while carried");
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(2, world.Orders.Count, "a place beyond 32 is sent");
            brain.OrderAt("attack", 540f, 0f);
            Expect.Equal(3, world.Orders.Count, "another order string is sent");
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(4, world.Orders.Count, "and the first again after it");

            world.PlayerOrders(unit.Handle, "attack", 0f, 0f);
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(5, world.Orders.Count, "sent again once another order took over");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(6, world.Orders.Count, "sent again when idle away from the place");
            world.PlaceUnit(unit.Handle, 500f, 0f);
            world.SetCurrentOrder(unit.Handle, 0);
            Expect.True(brain.OrderAt("move", 540f, 0f), "done, it gives true");
            Expect.Equal(6, world.Orders.Count, "and is dropped when idle within 64 of the place");

            world.SetCurrentOrder(unit.Handle, MoveId);
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(6, world.Orders.Count, "dropped while carried");
            unit.Owner = Player.Get(2);
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(7, world.Orders.Count, "sent again after an owner change");
            brain.OrderAt("move", 540f, 0f);
            Expect.Equal(7, world.Orders.Count, "then dropped again");
            Expect.Equal("point move", world.Orders[6], "through the unit's OrderAt");
            Expect.Near(540f, world.OrderPoints[12], "at the place");
        }

        /// <summary>An order the unit reads idle just after it was taken, as a move cancelled in its own order event: a repeat with the unit idle short of the place is sent, not dropped as carried.</summary>
        public static void IdleJustAfter(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            world.Orders.Clear();

            // The fake knows no "blink", so the unit reads idle at once after it, as after an order cancelled in its event.
            Expect.True(brain.OrderAt("blink", 500f, 0f), "taken");
            Expect.Equal("", unit.CurrentOrder, "and idle at once");
            brain.OrderAt("blink", 500f, 0f);
            Expect.Equal(2, world.Orders.Count, "the repeat is sent, as the unit is idle far from the place");
        }

        /// <summary>A unit idle short of the place, as in a jammed crowd, gets the repeat once; idle again where it stood then, the repeat is dropped; idle 100 away, it is sent again. An order that is no repeat, or a change of owner, forgets that spot.</summary>
        public static void SecondTry(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            world.Orders.Clear();

            brain.OrderAt("move", 1000f, 0f);
            world.PlaceUnit(unit.Handle, 800f, 0f);
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(2, world.Orders.Count, "idle 200 short, the repeat is sent once");
            world.SetCurrentOrder(unit.Handle, 0);
            Expect.True(brain.OrderAt("move", 1000f, 0f), "idle again at that spot, it gives true");
            Expect.Equal(2, world.Orders.Count, "and is dropped");
            world.PlaceUnit(unit.Handle, 700f, 0f);
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(3, world.Orders.Count, "idle 100 away, it is sent again");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(3, world.Orders.Count, "and dropped at that spot");

            brain.OrderAt("move", 2000f, 0f);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(5, world.Orders.Count, "two orders that are no repeats");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(6, world.Orders.Count, "forget the spot: idle there, the repeat is sent");

            unit.Owner = Player.Get(2);
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(7, world.Orders.Count, "an owner change sends it");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(8, world.Orders.Count, "and forgets the spot, so the next idle repeat is a second try");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderAt("move", 1000f, 0f);
            Expect.Equal(8, world.Orders.Count, "then dropped");
        }

        /// <summary>Target and plain orders: a new target is sent, a repeat is dropped while carried, sent again when idle with the target alive, and dropped once it is dead; a plain order is done once idle; a refused order is not remembered.</summary>
        public static void TargetAndPlainOrders(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            var foe = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            var other = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            world.Orders.Clear();

            brain.OrderOn("attack", foe);
            brain.OrderOn("attack", foe);
            Expect.Equal(1, world.Orders.Count, "a repeat on the same target is dropped while carried");
            brain.OrderOn("attack", other);
            Expect.Equal(2, world.Orders.Count, "a new target is sent");
            Expect.Same(other.Handle, world.OrderTargets[1], "on it");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.OrderOn("attack", other);
            Expect.Equal(3, world.Orders.Count, "sent again when idle and the target lives");
            other.Kill();
            world.SetCurrentOrder(unit.Handle, 0);
            Expect.True(brain.OrderOn("attack", other), "a dead target's repeat gives true");
            Expect.Equal(3, world.Orders.Count, "and is dropped when idle");

            brain.Order("holdposition");
            brain.Order("holdposition");
            Expect.Equal(4, world.Orders.Count, "a plain repeat is dropped while carried");
            world.SetCurrentOrder(unit.Handle, 0);
            brain.Order("holdposition");
            Expect.Equal(4, world.Orders.Count, "and once idle, as it has neither place nor target");
            Expect.Equal("immediate holdposition", world.Orders[3], "through the unit's Order");

            world.OrderAnswer = false;
            Expect.True(!brain.Order("stop"), "a refused order gives false");
            world.OrderAnswer = true;
            brain.Order("stop");
            Expect.Equal(6, world.Orders.Count, "and is not remembered, so the next is sent");
            unit.Order("stop");
            Expect.Equal(7, world.Orders.Count, "the unit's own Order sends a repeat");
        }
    }
}
