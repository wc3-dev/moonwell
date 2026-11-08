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

        /// <summary>A unit removed inside leaves its transport's Cargo with no reaction; a transport removed lets out what the game no longer has loaded, with its reactions.</summary>
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

            world.LoadedIn.Remove(other.Handle);
            zeppelin.Remove();
            ExpectLog(new[] { "cargo unloaded, transport set False" }, "the one the game let out comes out");
            Expect.Same(null, other.Transport, "and has no transport");
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
