using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check deliveries (J109): a rise of a player's gathered gold or lumber, taken by the worker the game orders back to harvest in the same instant, at the building its way back went to, on the building, its components and the map's components.</summary>
    public static class DeliveryCases
    {
        private static readonly int MillId = ObjectTypes.FourCC("h013");
        private static readonly int Peasant = ObjectTypes.FourCC("hpea");

        /// <summary>A building class that logs what was brought in.</summary>
        private sealed class Mill : Unit
        {
            public override void OnDelivered(Unit worker, int gold, int lumber)
            {
                TestLog.Lines.Add("mill " + Names.IdText(worker.TypeId) + " " + gold + " " + lumber);
            }
        }

        /// <summary>A unit component that logs a delivery.</summary>
        private sealed class Ledger : UnitComponent
        {
            public override void OnDelivered(Unit worker, int gold, int lumber)
            {
                TestLog.Lines.Add("component");
            }
        }

        /// <summary>A game component that logs each delivery's building type and amounts.</summary>
        private sealed class Watch : GameComponent
        {
            public override void OnDelivered(Unit worker, Unit building, int gold, int lumber)
            {
                TestLog.Lines.Add("game " + Names.IdText(building.TypeId) + " " + gold + " " + lumber);
            }
        }

        /// <summary>A player component that logs its slot.</summary>
        private sealed class PlayerWatch : PlayerComponent
        {
            public override void OnDelivered(Unit worker, Unit building, int gold, int lumber)
            {
                TestLog.Lines.Add("player " + Player.Slot);
            }
        }

        /// <summary>A structure of a slot at a place.</summary>
        private static unit Structure(FakeWorld world, int typeId, int slot, float x, float y)
        {
            var made = world.NewUnit(typeId, slot: slot);
            world.MakeStructure(made);
            world.PlaceUnit(made, x, y);
            return made;
        }

        /// <summary>A delivery runs on the building's class, its components, the game's components and the worker's player's, not another player's; the order events reach it through Inbound. A building class overriding OnDelivered listens for the gathered states and the orders.</summary>
        public static void ReactionOrder(FakeWorld world)
        {
            ObjectTypes.Unit(MillId, () => new Mill());
            Expect.True(world.Listening.Contains(GameEvent.Delivery) && world.Listening.Contains(GameEvent.Order), "listens for the gathered states and the orders");
            Game.Instance.Add(new Watch());
            Player.Get(0).Add(new PlayerWatch());
            Player.Get(1).Add(new PlayerWatch());
            var mill = Unit.Of(Structure(world, MillId, 0, 500f, 0f))!;
            mill.Add(new Ledger());
            var worker = world.NewUnit(Peasant, slot: 0);
            TestLog.Lines.Clear();
            Inbound.Ordered(worker, Delivering.ReturnOrder, OrderKind.Target, 500f, 0f, mill.Handle, null, null, 1);
            Delivering.Gathered(0, 0, 10);
            Inbound.Ordered(worker, Delivering.HarvestOrder, OrderKind.Target, 900f, 0f, null, null, null, 1);
            Expect.Equal("mill hpea 0 10,component,game h013 0 10,player 0", string.Join(",", TestLog.Lines), "the reactions");
            TestLog.Lines.Clear();
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal(0, TestLog.Lines.Count, "a harvest order with nothing gathered is no delivery");
        }

        /// <summary>Gold or lumber that no worker takes in its instant (a wisp's, an acolyte's) runs nothing, and is not given to a later worker.</summary>
        public static void UntakenIsLetGo(FakeWorld world)
        {
            Game.Instance.Add(new Watch());
            var hall = Structure(world, ObjectTypes.FourCC("htow"), 0, 0f, 0f);
            var worker = world.NewUnit(Peasant, slot: 0);
            TestLog.Lines.Clear();
            Delivering.Gathered(0, 0, 5);
            world.Advance(0.1f);
            Delivering.Ordered(worker, Delivering.ReturnOrder, hall);
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal(0, TestLog.Lines.Count, "nothing after the instant");
            Delivering.Gathered(0, -20, 0);
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal(0, TestLog.Lines.Count, "a fall is no delivery");
        }

        /// <summary>A worker whose way back was not seen delivers to the nearest structure of its player's near it; another player's rise is not its own.</summary>
        public static void NearestBuildingAndOwnPlayer(FakeWorld world)
        {
            Game.Instance.Add(new Watch());
            Structure(world, ObjectTypes.FourCC("hhou"), 0, 300f, 0f);
            Structure(world, ObjectTypes.FourCC("htow"), 0, 150f, 0f);
            Structure(world, ObjectTypes.FourCC("ogre"), 1, 50f, 0f);
            var worker = world.NewUnit(Peasant, slot: 0);
            world.PlaceUnit(worker, 0f, 0f);
            TestLog.Lines.Clear();
            Delivering.Gathered(1, 10, 0);
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal(0, TestLog.Lines.Count, "another player's gold");
            Delivering.Gathered(0, 10, 0);
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal("game htow 10 0", string.Join(",", TestLog.Lines), "the nearest of its own player's");
        }

        /// <summary>A right-click on a building of its own player's is a way back too; a right-click on another's is not.</summary>
        public static void RightClickOnOwnBuilding(FakeWorld world)
        {
            Game.Instance.Add(new Watch());
            var mill = Structure(world, ObjectTypes.FourCC("hlum"), 0, 2000f, 0f);
            var theirs = Structure(world, ObjectTypes.FourCC("ofor"), 1, 2000f, 500f);
            var worker = world.NewUnit(Peasant, slot: 0);
            world.PlaceUnit(worker, 0f, 0f);
            TestLog.Lines.Clear();
            Delivering.Ordered(worker, 851971, mill);
            Delivering.Ordered(worker, 851971, theirs);
            Delivering.Gathered(0, 0, 10);
            Delivering.Ordered(worker, Delivering.HarvestOrder, null);
            Expect.Equal("game hlum 0 10", string.Join(",", TestLog.Lines), "the right-clicked building");
        }
    }
}
