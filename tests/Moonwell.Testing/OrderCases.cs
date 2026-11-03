namespace Moonwell.Testing
{
    /// <summary>Cases for orders: the reactions, refusing and replacing an order, the last orders and the current one, on the fake game.</summary>
    public static class OrderCases
    {
        /// <summary>The type id the order cases register OrderedUnit for.</summary>
        private static readonly int OrderedType = ObjectTypes.FourCC("h0or");

        /// <summary>Fails unless the log is exactly these lines.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(string.Join(", ", expected), string.Join(", ", TestLog.Lines.ToArray()), what);
        }

        /// <summary>Registers OrderedUnit and NestingUnit, adds a game and a Player 1 watcher, and makes an OrderedUnit of Player 1 with an OrderedComponent; the log is cleared.</summary>
        private static OrderedUnit Arena(FakeWorld world, out OrderedGame game)
        {
            ObjectTypes.Unit(OrderedType, () => new OrderedUnit());
            ObjectTypes.Unit(ObjectTypes.FourCC("h0ns"), () => new NestingUnit());
            game = new OrderedGame();
            Game.Instance.Add(game);
            Player.Get(0).Add(new OrderedPlayer());
            var unit = (OrderedUnit)Unit.Of(world.NewUnit(OrderedType, slot: 0))!;
            unit.Add(new OrderedComponent());
            TestLog.Lines.Clear();
            return unit;
        }

        /// <summary>The constants are the game's order strings.</summary>
        public static void Constants(FakeWorld world)
        {
            Expect.Equal("move", Orders.Move, "Move");
            Expect.Equal("smart", Orders.Smart, "Smart");
            Expect.Equal("holdposition", Orders.HoldPosition, "HoldPosition");
            Expect.Equal("returnresources", Orders.ReturnResources, "ReturnResources");
            Expect.Equal("attackground", Orders.AttackGround, "AttackGround");
        }

        /// <summary>An order runs the unit's class, its components, the game's components, then the unit's player's.</summary>
        public static void ReactionsOrder(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            ExpectLog(new[] { "unit move", "component move", "game move", "player move" }, "the reactions in order");
        }

        /// <summary>A place order has its place; a target order its target and the target's place; one with neither the unit's place. A target Moonwell had not taken in is taken in.</summary>
        public static void PlaceAndTarget(FakeWorld world)
        {
            var unit = Arena(world, out var game);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            var place = game.Heard!;
            Expect.True(place.Kind == OrderKind.Place, "a place order");
            Expect.Near(100f, place.X, "its X");
            Expect.Near(200f, place.Y, "its Y");
            Expect.True(place.Target == null, "no target");
            Expect.Same(unit, place.Unit, "the unit");
            Expect.Equal(851986, place.Id, "the id");
            var other = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.PlaceUnit(other, 300f, 400f);
            world.PlayerOrdersOn(unit.Handle, Orders.Attack, other);
            var target = game.Heard!;
            Expect.True(target.Kind == OrderKind.Target, "a target order");
            Expect.Same(Unit.Of(other), target.Target, "the target, taken in");
            Expect.Near(300f, target.X, "the target's X");
            Expect.Near(400f, target.Y, "the target's Y");
            world.PlaceUnit(unit.Handle, 50f, 60f);
            world.PlayerOrdersNone(unit.Handle, Orders.HoldPosition);
            var none = game.Heard!;
            Expect.True(none.Kind == OrderKind.None, "an order with neither");
            Expect.Near(50f, none.X, "the unit's X");
            Expect.Near(60f, none.Y, "the unit's Y");
            Expect.Equal("holdposition", none.Name, "its name");
        }

        /// <summary>An order given in code runs its reactions inside the call, as the game fires the event inside it.</summary>
        public static void InsideTheCall(FakeWorld world)
        {
            var unit = Arena(world, out _);
            unit.OrderAt(Orders.Move, 1f, 2f);
            TestLog.Lines.Add("returned");
            ExpectLog(new[] { "unit move", "component move", "game move", "player move", "returned" }, "heard before the call returned");
        }

        /// <summary>The game's own orders, the stunned order and undefend, run nothing.</summary>
        public static void GamesOwn(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrdersId(unit.Handle, 851973, OrderKind.Target);
            world.PlayerOrdersId(unit.Handle, 852056, OrderKind.None);
            ExpectLog(new string[0], "nothing heard");
        }

        /// <summary>With no order reaction written, the world is not asked to listen for orders.</summary>
        public static void OnlyWhenWanted(FakeWorld world)
        {
            Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0));
            Expect.True(!world.Listening.Contains(Moonwell.Hosting.GameEvent.Order), "not listening");
            ObjectTypes.Unit(OrderedType, () => new OrderedUnit());
            Unit.Of(world.NewUnit(OrderedType, slot: 0));
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Order), "listening once a unit writes OnOrdered");
        }

        /// <summary>A cancelled order is refused by giving the unit's last order back, inside the event; no reaction hears it, and the cancelled order is not kept.</summary>
        public static void CancelGivesBack(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            var patrol = unit.LastOrder;
            TestLog.Lines.Clear();
            world.Orders.Clear();
            world.OrderPoints.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            ExpectLog(new[] { "unit move", "component move", "game move", "cancel", "player move" }, "the move heard and cancelled, the patrol unheard");
            Expect.Equal("point patrol", string.Join(", ", world.Orders.ToArray()), "the patrol given back");
            Expect.Near(10f, world.OrderPoints[0], "to its X");
            Expect.Near(20f, world.OrderPoints[1], "to its Y");
            Expect.Same(patrol, unit.LastOrder, "the cancelled move not kept, the patrol not kept again");
        }

        /// <summary>A cancelled order of a unit with no last order is refused by a stop a moment later, which no reaction hears.</summary>
        public static void CancelWithNone(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.Equal(0, world.Orders.Count, "nothing given inside the event");
            TestLog.Lines.Clear();
            world.Advance(0.01f);
            Expect.Equal("immediate stop", string.Join(", ", world.Orders.ToArray()), "a stop a moment later");
            ExpectLog(new string[0], "the stop unheard");
            Expect.True(unit.LastOrder == null, "nothing kept");
        }

        /// <summary>A reaction cancels an order and another replaces it: the replacement holds, and nothing is given back.</summary>
        public static void CancelAndReplace(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            Game.Instance.Add(new ReplacesMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            world.Advance(0.01f);
            Expect.Equal("point move", string.Join(", ", world.Orders.ToArray()), "only the replacement");
            Expect.Near(-100f, unit.LastOrder!.X, "the replacement kept");
        }

        /// <summary>The reactions after a cancel still run, and read IsCancelled.</summary>
        public static void OtherReactionsRun(FakeWorld world)
        {
            Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            Game.Instance.Add(new ReadsCancelled());
            var unit = (OrderedUnit)Unit.Of(world.NewUnit(OrderedType, slot: 0))!;
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.True(TestLog.Lines.Contains("cancelled true"), "a later reaction reads the cancel");
        }

        /// <summary>A unit removed by its own order reaction runs no later reaction, and nothing is given back.</summary>
        public static void RemovedInReaction(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h0rm"), () => new RemovingUnit());
            Game.Instance.Add(new OrderedGame());
            Game.Instance.Add(new CancelsMovesEast());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("h0rm"), slot: 0))!;
            TestLog.Lines.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            world.Advance(0.01f);
            ExpectLog(new[] { "removing" }, "nothing after the removal");
            Expect.Equal(0, world.Orders.Count, "nothing given");
        }

        /// <summary>After a stop, a refused order gives nothing old back: the unit was standing, and a stop a moment later keeps it so.</summary>
        public static void StopThenRefuse(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Move, -50f, 0f);
            world.PlayerOrdersNone(unit.Handle, Orders.Stop);
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.Equal(0, world.Orders.Count, "the old move not given back");
            world.Advance(0.01f);
            Expect.Equal("immediate stop", string.Join(", ", world.Orders.ToArray()), "a stop a moment later");
        }

        /// <summary>After an order not worth giving again that replaced what the unit did (a build, by unit type), a refused order gives nothing old back.</summary>
        public static void BuildThenRefuse(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.PlayerOrdersId(unit.Handle, ObjectTypes.FourCC("hhou"), OrderKind.Place);
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            world.Advance(0.01f);
            Expect.Equal("immediate stop", string.Join(", ", world.Orders.ToArray()), "a stop, not the patrol");
        }

        /// <summary>An instant order (using an item, a toggle) does not change what the unit does: a refused order after it gives the earlier one back.</summary>
        public static void InstantThenRefuse(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.PlayerOrdersId(unit.Handle, 852008, OrderKind.None);
            world.PlayerOrdersNone(unit.Handle, "immolation");
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.Equal("point patrol", string.Join(", ", world.Orders.ToArray()), "the patrol given back");
        }

        /// <summary>A stop given inside an order reaction, which the game does not hold there, is made to hold by Moonwell's own stop a moment later, with the order cancelled or not.</summary>
        public static void StopInReaction(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new StopsMovesEast(false));
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            world.Advance(0.01f);
            Expect.Equal("immediate stop, immediate stop", string.Join(", ", world.Orders.ToArray()), "the reaction's stop, then Moonwell's a moment later");
            Expect.Equal("patrol", unit.LastOrder!.Name, "the move not kept");
        }

        /// <summary>Cancel and a stop in the reaction: a stop a moment later too, not the earlier order.</summary>
        public static void CancelAndStop(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new StopsMovesEast(true));
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            world.Advance(0.01f);
            Expect.Equal("immediate stop, immediate stop", string.Join(", ", world.Orders.ToArray()), "the stops, no patrol");
        }

        /// <summary>A target order is given back on its target; when the target died, a stop a moment later instead.</summary>
        public static void TargetGivenBack(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Game.Instance.Add(new CancelsMovesEast());
            var other = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.PlayerOrdersOn(unit.Handle, Orders.Attack, other);
            world.Orders.Clear();
            TestLog.Lines.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.Equal("target attack", string.Join(", ", world.Orders.ToArray()), "the attack given back");
            Expect.Same(Unit.Of(other), Unit.Of((unit)world.OrderTargets[world.OrderTargets.Count - 1]), "on its target");
            Expect.True(!TestLog.Lines.Contains("unit attack"), "unheard");
            Unit.Of(other)!.Kill();
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 600f, 0f);
            world.Advance(0.01f);
            Expect.Equal("immediate stop", string.Join(", ", world.Orders.ToArray()), "a dead target: a stop instead");
        }

        /// <summary>A patrol under way reads the game's nameless 851991 (seen 11-03): CurrentOrder names it patrol.</summary>
        public static void PatrolUnderWay(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.SetCurrentOrder(unit.Handle, 851991);
            Expect.Equal("patrol", unit.CurrentOrder, "a patrol under way");
        }

        /// <summary>A Shift-queued order is marked queued; cancelled, the earlier order is given back, as for any.</summary>
        public static void QueuedCancel(FakeWorld world)
        {
            var unit = Arena(world, out var game);
            Game.Instance.Add(new CancelsMovesEast());
            world.PlayerOrders(unit.Handle, Orders.Patrol, 10f, 20f);
            world.Orders.Clear();
            world.SetQueued(unit.Handle, 2);
            world.PlayerOrders(unit.Handle, Orders.Move, 500f, 0f);
            Expect.True(game.Heard!.IsQueued, "queued");
            Expect.Equal("point patrol", string.Join(", ", world.Orders.ToArray()), "the earlier given back");
            world.PlayerOrders(unit.Handle, Orders.Move, -5f, 0f);
            Expect.True(!game.Heard!.IsQueued, "a fresh order is not");
        }

        /// <summary>The last three orders worth giving again are kept, the newest first, each reaching back with Previous.</summary>
        public static void LastThree(FakeWorld world)
        {
            var unit = Arena(world, out _);
            var other = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.PlayerOrders(unit.Handle, Orders.Move, 1f, 1f);
            world.PlayerOrders(unit.Handle, Orders.Patrol, 2f, 2f);
            world.PlayerOrdersOn(unit.Handle, Orders.Attack, other);
            world.PlayerOrdersNone(unit.Handle, Orders.HoldPosition);
            var last = unit.LastOrder!;
            Expect.Equal("holdposition", last.Name, "the newest");
            Expect.Equal("attack", last.Previous!.Name, "the one before");
            Expect.Equal("patrol", last.Previous!.Previous!.Name, "the oldest kept");
            Expect.True(last.Previous!.Previous!.Previous == null, "only three kept");
            Expect.Equal("patrol", unit.Heard!.Previous!.Previous!.Name, "a reaction reads the order's Previous");
        }

        /// <summary>Stop, toggles and item slots are not kept: they are not worth giving again.</summary>
        public static void NotWorthIt(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrders(unit.Handle, Orders.Move, 1f, 1f);
            var move = unit.LastOrder;
            world.PlayerOrdersNone(unit.Handle, Orders.Stop);
            world.PlayerOrdersNone(unit.Handle, "defend");
            world.PlayerOrdersNone(unit.Handle, "immolation");
            world.PlayerOrdersId(unit.Handle, 852008, OrderKind.None);
            world.PlayerOrdersId(unit.Handle, 852512, OrderKind.None);
            world.PlayerOrdersId(unit.Handle, 852166, OrderKind.None);
            Expect.Same(move, unit.LastOrder, "the move is still the last");
            Expect.Equal(7, TestLog.Lines.Count / 4, "all seven were heard");
        }

        /// <summary>Reissue gives the last order again and gives the game's answer.</summary>
        public static void Reissue(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            world.Orders.Clear();
            world.OrderPoints.Clear();
            Expect.True(unit.Reissue(), "the game's answer");
            Expect.Equal("point move", string.Join(", ", world.Orders.ToArray()), "the move given again");
            Expect.Near(100f, world.OrderPoints[0], "to its X");
            Expect.Near(200f, world.OrderPoints[1], "to its Y");
            world.OrderAnswer = false;
            Expect.True(!unit.Reissue(), "the game's no");
        }

        /// <summary>Reissue of a target order whose target died answers false and gives no order; so does Reissue of a dead unit, and of a unit with no last order.</summary>
        public static void ReissueGone(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Expect.True(!unit.Reissue(), "no last order");
            var other = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.PlayerOrdersOn(unit.Handle, Orders.Attack, other);
            Unit.Of(other)!.Kill();
            world.Orders.Clear();
            Expect.True(!unit.Reissue(), "a dead target");
            Expect.Equal(0, world.Orders.Count, "no order given");
            world.PlayerOrders(unit.Handle, Orders.Move, 1f, 1f);
            unit.Kill();
            Expect.True(!unit.Reissue(), "a dead unit");
            Expect.Equal(0, world.Orders.Count, "still no order given");
        }

        /// <summary>A spell order whose spell was cast is done: Reissue does not cast it again.</summary>
        public static void SpellDone(FakeWorld world)
        {
            var unit = Arena(world, out _);
            var other = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0);
            world.PlayerOrdersOn(unit.Handle, "holybolt", other);
            Inbound.SpellEffect(unit.Handle, ObjectTypes.FourCC("AHhb"), other, 0f, 0f);
            world.Orders.Clear();
            Expect.True(!unit.Reissue(), "not cast again");
            Expect.Equal(0, world.Orders.Count, "no order given");
        }

        /// <summary>The order the game gives back when a stun ends, the same as the one before it, is not heard and not kept again.</summary>
        public static void AfterStun(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            var move = unit.LastOrder;
            TestLog.Lines.Clear();
            world.PlayerOrdersId(unit.Handle, 851973, OrderKind.Target);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            ExpectLog(new string[0], "the move given back is not heard");
            Expect.Same(move, unit.LastOrder, "and not kept again");
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            Expect.Equal(4, TestLog.Lines.Count, "the same move given after that is heard");
        }

        /// <summary>A different order after a stun is heard.</summary>
        public static void AfterStunOther(FakeWorld world)
        {
            var unit = Arena(world, out _);
            world.PlayerOrders(unit.Handle, Orders.Move, 100f, 200f);
            world.PlayerOrdersId(unit.Handle, 851973, OrderKind.Target);
            TestLog.Lines.Clear();
            world.PlayerOrders(unit.Handle, Orders.Move, 300f, 200f);
            Expect.Equal(4, TestLog.Lines.Count, "heard");
            Expect.Near(300f, unit.LastOrder!.X, "and kept");
        }

        /// <summary>CurrentOrder names what the game says the unit does now; "" when idle.</summary>
        public static void Current(FakeWorld world)
        {
            var unit = Arena(world, out _);
            Expect.Equal("", unit.CurrentOrder, "idle");
            world.PlayerOrders(unit.Handle, Orders.Move, 1f, 1f);
            Expect.Equal("move", unit.CurrentOrder, "moving");
            unit.Stop();
            Expect.Equal("", unit.CurrentOrder, "idle after a stop");
        }

        /// <summary>An order given inside an order reaction runs its reactions nested; past 16 deep it is reported and runs nothing.</summary>
        public static void Nested(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h0ns"), () => new NestingUnit());
            var unit = (NestingUnit)Unit.Of(world.NewUnit(ObjectTypes.FourCC("h0ns"), slot: 0))!;
            world.PlayerOrders(unit.Handle, Orders.Move, 0f, 0f);
            Expect.Equal(16, unit.Heard, "16 reactions, one for each depth");
            Expect.Equal(1, world.Logged.Count, "one line logged");
            Expect.True(world.Logged[0].StartsWith("Moonwell: Moonwell.Orders"), "reported: " + world.Logged[0]);
        }
    }
}
