using System.Collections.Generic;
using Moonwell.AI;
using static Moonwell.AI.Nodes;

namespace Moonwell.Testing
{
    // shortcut: the file sorts after Fact.cs on purpose, as TreeNodeCases.cs does: a file before Fact.cs that names a Fact<T> makes it
    // before Fact.cs's own imports are filled, stopping the script at load. Rename freely once Fact<T> no longer reads its file's imports
    // when made.
    /// <summary>Cases for the behaviour trees' unit actions: MoveTo, Attack, Cast, FollowRoute and UseItem, each ordering the unit through its brain.</summary>
    public static class TreeUnitCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A written unit, which may be null.</summary>
        private static readonly Fact<Unit?> Raider = new Fact<Unit?>("raider");

        /// <summary>The game's id of "holybolt" in the fake, a spell on a unit.</summary>
        private const int HolyBoltId = 852092;

        /// <summary>A unit of player 0 at a place, with a brain that never thinks by itself, for a case to run trees on pass by pass.</summary>
        private static StepBrain BrainAt(FakeWorld world, float x, float y)
        {
            var handle = world.NewUnit(Footman, slot: 0);
            world.PlaceUnit(handle, x, y);
            var brain = Unit.Of(handle)!.Add(new StepBrain());
            world.Orders.Clear();
            return brain;
        }

        /// <summary>Checks a status.</summary>
        private static void Is(Status expected, Status actual, string what)
        {
            Expect.Equal((int)expected, (int)actual, what);
        }

        /// <summary>A node on a game brain's tree: Failed, with its Unit reported once and no order given.</summary>
        private static void RefusedOnAGameBrain(FakeWorld world, Node node, string name)
        {
            world.Logged.Clear();
            var orders = world.Orders.Count;
            Is(Status.Failed, new Tree(Game.Instance.Add(new StepGameBrain()), node).Run(), "Failed on a game brain's tree");
            Expect.Equal(1, world.Logged.Count, "reported once");
            Expect.Equal("Moonwell: " + name + ".Unit failed: the tree is run by a player or game brain, which has no unit", world.Logged[0], "the logged line");
            Expect.Equal(orders, world.Orders.Count, "and no order given");
        }

        /// <summary>MoveTo orders a move once while the unit carries it, follows a place that moves beyond 32, succeeds within 64 with no order, fails once a jammed unit has had its one more try or the order is refused, and fails for a dead unit.</summary>
        public static void MoveTo(FakeWorld world)
        {
            var brain = BrainAt(world, 0f, 0f);
            var unit = brain.Unit;
            var toX = 500f;
            var tree = new Tree(brain, Nodes.MoveTo(() => toX, () => 0f));

            Is(Status.Running, tree.Run(), "it walks");
            Expect.Equal("point move", string.Join(", ", world.Orders), "the move");
            Is(Status.Running, tree.Run(), "it walks on");
            toX = 520f;
            Is(Status.Running, tree.Run(), "the place moved within 32");
            Expect.Equal(1, world.Orders.Count, "no repeat reaches the game while the unit carries it");
            toX = 600f;
            tree.Run();
            Expect.Equal(2, world.Orders.Count, "the place moved beyond 32: sent again");
            Expect.Near(600f, world.OrderPoints[2], "to the place read at that pass");

            world.PlaceUnit(unit.Handle, 560f, 0f);
            Is(Status.Succeeded, tree.Run(), "within 64");
            Expect.Equal(2, world.Orders.Count, "with no order");

            world.PlaceUnit(unit.Handle, 0f, 0f);
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Running, tree.Run(), "idle far from the place, the brain sends the repeat once");
            Expect.Equal(3, world.Orders.Count, "sent");
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Failed, tree.Run(), "idle again where it stood, jammed: the repeat is dropped and the unit is idle just after");
            Expect.Equal(3, world.Orders.Count, "nothing sent");

            world.OrderAnswer = false;
            toX = 2000f;
            Is(Status.Failed, tree.Run(), "a refused order");
            world.OrderAnswer = true;
            unit.Kill();
            Is(Status.Failed, tree.Run(), "a dead unit");
            Expect.Equal(4, world.Orders.Count, "and no order from a dead unit");

            RefusedOnAGameBrain(world, Nodes.MoveTo(() => 0f, () => 0f), "MoveToNode");
        }

        /// <summary>Attack orders the attack once while carried, follows a new target, succeeds once the target dies at a later pass, fails for a null target, a target dead at its first pass, a unit that gave up, and a refused order; and reads a fact's target.</summary>
        public static void Attack(FakeWorld world)
        {
            var brain = BrainAt(world, 0f, 0f);
            var unit = brain.Unit;
            var foe = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            var other = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            Unit? aim = foe;
            var tree = new Tree(brain, Nodes.Attack(() => aim));

            Is(Status.Running, tree.Run(), "it attacks");
            Is(Status.Running, tree.Run(), "and goes on");
            Expect.Equal("target attack", string.Join(", ", world.Orders), "once: the repeat is dropped while carried");
            aim = other;
            Is(Status.Running, tree.Run(), "a new target");
            Expect.Same(other.Handle, world.OrderTargets[1], "is followed");
            other.Kill();
            Is(Status.Succeeded, tree.Run(), "the target died at a later pass");
            Is(Status.Failed, tree.Run(), "started again on a dead target");
            aim = null;
            Is(Status.Failed, tree.Run(), "a null target");
            Expect.Equal(2, world.Orders.Count, "neither ordered");

            aim = foe;
            Is(Status.Running, tree.Run(), "at the foe again");
            Expect.Equal(3, world.Orders.Count, "sent, as the last was on another target");
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Failed, tree.Run(), "idle with the target alive: it gave up");
            Expect.Equal(3, world.Orders.Count, "with no order");
            world.OrderAnswer = false;
            Is(Status.Failed, tree.Run(), "a refused order");
            world.OrderAnswer = true;

            var fromFact = new Tree(brain, Nodes.Attack(Raider));
            Is(Status.Failed, fromFact.Run(), "nothing holds the fact");
            Player.Get(0).Blackboard().Set(Raider, foe);
            Is(Status.Running, fromFact.Run(), "the player's fact, up the chain");
            Expect.Same(foe.Handle, world.OrderTargets[world.OrderTargets.Count - 1], "on its unit");

            RefusedOnAGameBrain(world, Nodes.Attack(() => foe), "AttackNode");
        }

        /// <summary>Cast orders the spell, then runs while the unit's order id is the one read just after and succeeds once it differs, or at once when it was 0; a second cast of the same plain spell is sent; a unit already carrying it is only watched; a null, dead or refused target fails.</summary>
        public static void Cast(FakeWorld world)
        {
            var brain = BrainAt(world, 0f, 0f);
            var unit = brain.Unit;
            var foe = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            var tree = new Tree(brain, Nodes.Cast("immolation"));

            Is(Status.Running, tree.Run(), "the unit carries the cast");
            Is(Status.Running, tree.Run(), "still");
            Expect.Equal("immediate immolation", string.Join(", ", world.Orders), "sent once");
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Succeeded, tree.Run(), "the unit stopped carrying it");
            Is(Status.Running, tree.Run(), "cast again");
            Expect.Equal(2, world.Orders.Count, "the second cast of the same plain spell is sent, not dropped as a repeat");

            var watch = new Tree(brain, Nodes.Cast("immolation"));
            Is(Status.Running, watch.Run(), "a unit already carrying the order string");
            Expect.Equal(2, world.Orders.Count, "is not ordered again");
            world.SetCurrentOrder(unit.Handle, 851986);
            Is(Status.Succeeded, watch.Run(), "another order took over");

            Is(Status.Succeeded, new Tree(brain, Nodes.Cast("blizzard", () => 100f, () => 0f)).Run(), "an order the unit reads 0 just after succeeds at once");
            Expect.Equal("point blizzard", world.Orders[2], "at the place");

            Unit? aim = null;
            var bolt = new Tree(brain, Nodes.Cast("holybolt", () => aim));
            Is(Status.Failed, bolt.Run(), "a null target");
            aim = foe;
            Is(Status.Running, bolt.Run(), "on the foe");
            Expect.Same(foe.Handle, world.OrderTargets[0], "the target reached the game");
            Expect.Equal(HolyBoltId, world.CurrentOrderOf(unit.Handle), "the unit carries it");
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Succeeded, bolt.Run(), "done");
            foe.Kill();
            Is(Status.Failed, bolt.Run(), "a dead target");
            Expect.Equal(4, world.Orders.Count, "neither the null nor the dead target was ordered");

            world.OrderAnswer = false;
            Is(Status.Failed, new Tree(brain, Nodes.Cast("immolation")).Run(), "a refused cast");
            world.OrderAnswer = true;
            unit.Remove();
            Is(Status.Failed, new Tree(brain, Nodes.Cast("immolation")).Run(), "a removed unit");

            RefusedOnAGameBrain(world, Nodes.Cast("immolation"), "CastNode");
        }

        /// <summary>FollowRoute walks the points in turn, skipping those within 64, and succeeds past the last; it takes up a new route after the point nearest the unit, the last when that is the nearest; Orders.Attack attack-moves; a null or empty route, a refused order and a unit idle just after fail.</summary>
        public static void FollowRoute(FakeWorld world)
        {
            var brain = BrainAt(world, 0f, 0f);
            var unit = brain.Unit;
            var east = new Route(new List<Vector2> { new Vector2(0f, 0f), new Vector2(500f, 0f), new Vector2(1000f, 0f) }, true);
            var north = new Route(new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 500f), new Vector2(0f, 1000f), new Vector2(0f, 1500f) }, true);
            Route? walked = east;
            var tree = new Tree(brain, Nodes.FollowRoute(() => walked));

            Is(Status.Running, tree.Run(), "it walks");
            Is(Status.Running, tree.Run(), "and goes on");
            Expect.Equal("point move", string.Join(", ", world.Orders), "once, to the point after the start, which is nearest");
            Expect.Near(500f, world.OrderPoints[0], "x");
            world.PlaceUnit(unit.Handle, 470f, 0f);
            Is(Status.Running, tree.Run(), "within 64 of it");
            Expect.Near(1000f, world.OrderPoints[2], "on to the next");
            world.PlaceUnit(unit.Handle, 990f, 0f);
            Is(Status.Succeeded, tree.Run(), "past the last");
            Expect.Equal(2, world.Orders.Count, "with no order");

            world.PlaceUnit(unit.Handle, 10f, 480f);
            walked = north;
            Is(Status.Running, tree.Run(), "a new route");
            Expect.Near(1000f, world.OrderPoints[5], "taken up after the point nearest the unit");
            world.PlaceUnit(unit.Handle, 0f, 0f);
            walked = east;
            tree.Run();
            Expect.Near(500f, world.OrderPoints[6], "another route object is new again");
            world.PlaceUnit(unit.Handle, 1000f, 1400f);
            walked = north;
            tree.Run();
            Expect.Near(1500f, world.OrderPoints[9], "the nearest is the last: it aims at the last");

            walked = null;
            Is(Status.Failed, tree.Run(), "a null route");
            walked = new Route(new List<Vector2>(), false);
            Is(Status.Failed, tree.Run(), "a route with no points");
            walked = east;
            world.OrderAnswer = false;
            Is(Status.Failed, tree.Run(), "a refused order");
            world.OrderAnswer = true;

            world.Orders.Clear();
            Is(Status.Running, new Tree(brain, Nodes.FollowRoute(() => east, Orders.Attack)).Run(), "an attack-move");
            Expect.Equal("point attack", world.Orders[0], "with the attack order");
            // The fake knows no "blink", so the unit reads idle at once after it, as after an order cancelled in its event.
            Is(Status.Failed, new Tree(brain, Nodes.FollowRoute(() => east, "blink")).Run(), "idle short of the point just after the order");

            RefusedOnAGameBrain(world, Nodes.FollowRoute(() => east), "FollowRouteNode");
        }

        /// <summary>UseItem uses the item through the unit's calls, then runs while the unit's order id is its slot's and succeeds once it differs; at a place and on a target too; a null or gone item or target and a refused use fail.</summary>
        public static void UseItem(FakeWorld world)
        {
            var brain = BrainAt(world, 0f, 0f);
            var unit = brain.Unit;
            var foe = Unit.Of(world.NewUnit(Footman, slot: 1))!;
            var potion = Item.Of(world.NewItem(ObjectTypes.FourCC("phea")))!;
            unit.Give(potion, 1);
            Item? used = potion;
            var tree = new Tree(brain, Nodes.UseItem(() => used));

            Is(Status.Running, tree.Run(), "the unit carries the use");
            Expect.Equal(852009, world.CurrentOrderOf(unit.Handle), "the second slot's order");
            Is(Status.Running, tree.Run(), "still");
            Expect.Equal("useitem", string.Join(", ", world.Orders), "used once");
            world.SetCurrentOrder(unit.Handle, 0);
            Is(Status.Succeeded, tree.Run(), "done");

            Is(Status.Running, new Tree(brain, Nodes.UseItem(() => potion, () => 10f, () => 20f)).Run(), "at a place");
            Expect.Near(20f, world.OrderPoints[1], "the place reached the game");
            Unit? aim = null;
            var onFoe = new Tree(brain, Nodes.UseItem(() => potion, () => aim));
            Is(Status.Failed, onFoe.Run(), "a null target");
            aim = foe;
            Is(Status.Running, onFoe.Run(), "on a target");
            Expect.Same(foe.Handle, world.OrderTargets[0], "the target reached the game");
            Expect.Equal("useitem, useitem point, useitem target", string.Join(", ", world.Orders), "the calls");

            used = null;
            Is(Status.Failed, tree.Run(), "a null item");
            used = potion;
            world.OrderAnswer = false;
            Is(Status.Failed, tree.Run(), "a refused use");
            world.OrderAnswer = true;
            potion.Remove();
            Is(Status.Failed, tree.Run(), "a gone item");
            Expect.Equal(4, world.Orders.Count, "only the refused use reached the game");

            RefusedOnAGameBrain(world, Nodes.UseItem(() => potion), "UseItemNode");
        }
    }
}
