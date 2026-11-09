namespace Moonwell.Testing
{
    /// <summary>Cases for transports (decision 219), on the fake game: the game's load event, and the order it gives the cargo on every way out, which tells Moonwell it came out.</summary>
    public static class TransportCases
    {
        private static readonly int CargoType = ObjectTypes.FourCC("h0cg");
        private static readonly int TransportType = ObjectTypes.FourCC("h0tr");

        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(string.Join(", ", expected), string.Join(", ", TestLog.Lines.ToArray()), what);
        }

        private static Unit Footman(FakeWorld world, int slot)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: slot))!;
        }

        /// <summary>A load runs the cargo's class, its components, the transport's class, its components, the game's components, then the cargo's player's; an unload the same, with Transport and Cargo already changed.</summary>
        public static void ReactionsOrder(FakeWorld world)
        {
            ObjectTypes.Unit(CargoType, () => new CargoUnit());
            ObjectTypes.Unit(TransportType, () => new TransportUnit());
            Game.Instance.Add(new TransportGame());
            Player.Get(0).Add(new TransportPlayer());
            Player.Get(1).Add(new TransportPlayer());
            var cargo = Unit.Of(world.NewUnit(CargoType, slot: 1))!;
            var transport = Unit.Of(world.NewUnit(TransportType, slot: 0))!;
            cargo.Add(new TransportComponent());
            transport.Add(new TransportComponent());
            TestLog.Lines.Clear();

            world.Load(cargo.Handle, transport.Handle);
            ExpectLog(new[] { "cargo loaded, transport set True", "component loaded", "transport got, holding 1", "component got", "game loaded", "player 1 loaded" }, "the load's reactions in order; the transport's player hears nothing");
            TestLog.Lines.Clear();

            world.LetOut(cargo.Handle);
            ExpectLog(new[] { "cargo unloaded, transport set False", "component unloaded", "transport lost, holding 0", "component lost", "game unloaded", "player 1 unloaded" }, "the unload's reactions in order");
        }

        /// <summary>Transport and Cargo follow the events, in load order; an order to a unit still loaded changes nothing.</summary>
        public static void ReadsFollow(FakeWorld world)
        {
            Moonwell.Runtime.ListenForTransports();
            var zeppelin = Footman(world, 0);
            var a = Footman(world, 0);
            var b = Footman(world, 0);
            world.Load(a.Handle, zeppelin.Handle);
            world.Load(b.Handle, zeppelin.Handle);
            world.PlayerOrdersId(a.Handle, 851972, OrderKind.None);

            Expect.Same(zeppelin, a.Transport, "a's transport");
            Expect.Equal(2, zeppelin.Cargo.Count, "two aboard");
            Expect.Same(a, zeppelin.Cargo[0], "in load order");
            world.LetOut(a.Handle);
            Expect.Same(null, a.Transport, "a is out");
            Expect.Equal(1, zeppelin.Cargo.Count, "one aboard");
            Expect.Same(b, zeppelin.Cargo[0], "b still aboard");
            Expect.Equal(0, a.Cargo.Count, "a unit that carries nothing");
        }

        /// <summary>A unit removed inside leaves its transport's Cargo with no reaction; a transport removed lets its cargo out first, with its reactions, though the game still has it aboard until RemoveUnit returns (seen 11-09), and the game's stop order after runs nothing more.</summary>
        public static void LeavingTheGame(FakeWorld world)
        {
            ObjectTypes.Unit(CargoType, () => new CargoUnit());
            Moonwell.Runtime.ListenForTransports();
            var zeppelin = Footman(world, 0);
            var inside = Unit.Of(world.NewUnit(CargoType, slot: 0))!;
            var other = Unit.Of(world.NewUnit(CargoType, slot: 0))!;
            world.Load(inside.Handle, zeppelin.Handle);
            world.Load(other.Handle, zeppelin.Handle);
            TestLog.Lines.Clear();

            inside.Remove();
            Expect.Equal(1, zeppelin.Cargo.Count, "the removed one is no longer aboard");
            ExpectLog(new string[0], "no reaction for a unit removed inside");

            zeppelin.Remove();
            ExpectLog(new[] { "cargo unloaded, transport set False" }, "the one aboard comes out");
            Expect.Same(null, other.Transport, "and has no transport");
            world.LetOut(other.Handle);
            ExpectLog(new[] { "cargo unloaded, transport set False" }, "the game's stop order after the removal runs nothing more");
        }

        /// <summary>A burrow as the game runs one (seen 11-09): the peon's stop and board orders before it is aboard, the load event with no undefend, then back to work's two stop orders inside the call, not loaded in either; one load and one unload (I60).</summary>
        public static void BurrowInAndOut(FakeWorld world)
        {
            ObjectTypes.Unit(CargoType, () => new CargoUnit());
            Moonwell.Runtime.ListenForTransports();
            var burrow = Footman(world, 0);
            var peon = Unit.Of(world.NewUnit(CargoType, slot: 0))!;
            TestLog.Lines.Clear();

            world.PlayerOrdersId(peon.Handle, 851972, OrderKind.None);
            world.PlayerOrdersId(peon.Handle, 852043, OrderKind.None);
            world.LoadedIn[peon.Handle] = burrow.Handle;
            Inbound.Loaded(peon.Handle, burrow.Handle);
            Expect.Same(burrow, peon.Transport, "the peon is in the burrow");
            world.LoadedIn.Remove(peon.Handle);
            world.PlayerOrdersId(peon.Handle, 851972, OrderKind.None);
            world.PlayerOrdersId(peon.Handle, 851972, OrderKind.None);
            ExpectLog(new[] { "cargo loaded, transport set True", "cargo unloaded, transport set False" }, "one load, one unload");
            Expect.Equal(0, burrow.Cargo.Count, "the burrow is empty");
        }

        /// <summary>A transport whose scope ends lets each living unit aboard out before its OnLeave and its components end, with the transport's reactions while it has not ended; one that died inside comes out with none (decision 219, I59).</summary>
        public static void ScopeDropsCargo(FakeWorld world)
        {
            ObjectTypes.Unit(CargoType, () => new CargoUnit());
            ObjectTypes.Unit(TransportType, () => new TransportUnit());
            Game.Instance.Add(new TransportGame());
            var scope = Game.Instance.Add(new TransportScope());
            var transport = Unit.Create(TransportType, Player.Get(0), 0.0f, 0.0f, scope);
            transport.Add(new TransportComponent());
            var living = Unit.Create(CargoType, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            var dead = Unit.Create(CargoType, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            world.Load(dead.Handle, transport.Handle);
            world.Load(living.Handle, transport.Handle);
            world.KillBy(dead.Handle, null);
            TestLog.Lines.Clear();

            scope.End();
            ExpectLog(new[] { "cargo unloaded, transport set False", "transport lost, holding 0", "component lost", "game unloaded", "transport leaves" }, "the living one comes out with every reaction before the transport leaves, the dead one with none");
            Expect.Same(null, living.Transport, "the living one has no transport");
            Expect.Same(null, dead.Transport, "nor the dead one");
            Expect.Equal(true, world.RemovedUnits.Contains(transport.Handle), "the transport removed");
        }

        /// <summary>The unit pool keeps neither a pooled unit inside a transport nor a pooled transport carrying units (decisions 219 and 220): either would come out of the pool still loaded.</summary>
        public static void PoolSkipsTransports(FakeWorld world)
        {
            var footman = ObjectTypes.FourCC("hfoo");
            ObjectTypes.Pooled(footman);
            Moonwell.Runtime.ListenForTransports();
            var zeppelin = Unit.Create(footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            var inside = Unit.Create(footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            var aboard = Unit.Create(footman, Player.Get(0), 0.0f, 0.0f, Game.Instance);
            world.Load(inside.Handle, zeppelin.Handle);
            world.Load(aboard.Handle, zeppelin.Handle);
            inside.Remove();
            zeppelin.Remove();

            Expect.Equal(0, world.PutAwayUnits.Count, "nothing kept");
            Expect.Equal(true, world.RemovedUnits.Contains(inside.Handle), "the loaded unit removed");
        }

        /// <summary>The cargo capacity reads and sets the game's; Load, Unload and UnloadAll are the game's orders.</summary>
        public static void CapacityAndOrders(FakeWorld world)
        {
            var zeppelin = Footman(world, 0);
            var footman = Footman(world, 0);
            zeppelin.CargoCapacity = 4;
            Expect.Equal(4, zeppelin.CargoCapacity, "the capacity set");
            zeppelin.Load(footman);
            zeppelin.Unload(footman);
            zeppelin.UnloadAll(100.0f, 200.0f);
            Expect.Equal("target load, target unload, point unloadall", string.Join(", ", world.Orders.ToArray()), "the orders given");
        }
    }
}
