using System;
using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Cases for a unit brain's full control (decisions 330 to 332): the unit's own picks and every order not from code are answered with hold position, and the unit is given back when it ends.</summary>
    public static class FullControlCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>The orders given through the world so far, in one line.</summary>
        private static string Given(FakeWorld world)
        {
            return string.Join(", ", world.Orders.ToArray());
        }

        /// <summary>How many times a unit's acquire event is registered.</summary>
        private static int Watches(FakeWorld world, Unit unit)
        {
            var count = 0;
            for (var i = 0; i < world.AcquireWatched.Count; i++)
            {
                if (world.AcquireWatched[i] == unit.Handle)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>A pick with the order reading 0 is held inside its event, as Moonwell's own order, and again on hold, whose order reads 0 too; one on the brain's attack-move is left; a brain that leans on the game answers nothing.</summary>
        public static void Acquire(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var ear = unit.Add(new OrderEar());
            var brain = unit.Add(new StepBrain { FullControl = true });
            world.Orders.Clear();

            world.Acquire(unit.Handle);
            Expect.Equal("immediate holdposition", Given(world), "an idle unit's pick is held inside its event");
            Expect.Equal(0, ear.Heard.Count, "as Moonwell's own order, which no reaction hears");
            Expect.Equal("", unit.CurrentOrder, "and the order reads 0 on hold");
            world.Acquire(unit.Handle);
            Expect.Equal(2, world.Orders.Count, "so a pick on hold is held again");
            BrainCases.Steps(world, 2);
            Expect.Equal(2, world.Orders.Count, "and Moonwell's hold is never answered");

            brain.OrderAt(Orders.Attack, 500f, 0f);
            world.Orders.Clear();
            world.Acquire(unit.Handle);
            BrainCases.Steps(world, 2);
            Expect.Equal(0, world.Orders.Count, "a pick on the brain's attack-move is left, and the brain's order is never answered");

            var leaning = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            leaning.Add(new StepBrain());
            world.Acquire(leaning.Handle);
            world.PlayerOrders(leaning.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 2);
            Expect.Equal(0, world.Orders.Count, "a brain that leans on the game answers nothing");
            Expect.Equal(0, Watches(world, leaning), "and its unit's picks are not watched");
        }

        /// <summary>An order not from code is heard by the reactions, then held at the next step, not inside its event, and the unit's brain forgets its last order and thinks in that step.</summary>
        public static void ForeignOrder(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var ear = unit.Add(new OrderEar());
            var brain = unit.Add(new StepBrain { FullControl = true });
            brain.OrderAt(Orders.Move, 500f, 0f);
            world.Orders.Clear();
            ear.Heard.Clear();

            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            Expect.Equal(0, world.Orders.Count, "not answered inside its event");
            Expect.Equal("move", string.Join(", ", ear.Heard.ToArray()), "the unit's reactions hear it as before");
            BrainCases.Steps(world, 1);
            Expect.Equal("immediate holdposition", Given(world), "held at the next step");
            Expect.Equal("1", string.Join(" ", brain.Thought), "and the brain thinks in that step");
            Expect.Equal(1, ear.Heard.Count, "the hold runs no reaction");
            Expect.True(brain.sentOrder == null, "and the brain forgets its last order, so its next reaches the game");
            BrainCases.Steps(world, 3);
            Expect.Equal(1, world.Orders.Count, "one answer");
        }

        /// <summary>Several orders not from code in one step, the game's own, an AI script's and a person's, make one answer and one think; one after it gets its own.</summary>
        public static void OneAnswer(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain { FullControl = true });
            world.Orders.Clear();

            world.PlayerOrdersId(unit.Handle, 851974, OrderKind.None);
            world.PlayerOrders(unit.Handle, Orders.Attack, 0f, 900f);
            world.PlayerOrdersNone(unit.Handle, Orders.Stop);
            BrainCases.Steps(world, 1);
            Expect.Equal("immediate holdposition", Given(world), "three in a step make one answer");
            Expect.Equal("1", string.Join(" ", brain.Thought), "and one think");

            BrainCases.Steps(world, 1);
            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 1);
            Expect.Equal(2, world.Orders.Count, "a later one gets its own");
            Expect.Equal("1 3", string.Join(" ", brain.Thought), "in the step after it");
        }

        /// <summary>An order from code after one not from code, before the step, drops its answer, one a reaction gives inside the event too; orders from code and the brain's own are never answered.</summary>
        public static void CodeOrders(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain { FullControl = true });
            world.Orders.Clear();

            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            unit.OrderAt(Orders.Move, 100f, 0f);
            BrainCases.Steps(world, 2);
            Expect.Equal("point move", Given(world), "an order from code drops the waiting answer");
            Expect.Equal(0, brain.Thought.Count, "and the brain is not woken");

            brain.OrderAt(Orders.Attack, 500f, 0f);
            unit.Order(Orders.Stop);
            unit.OrderOn(Orders.Attack, Unit.Of(world.NewUnit(Footman, slot: 1))!);
            BrainCases.Steps(world, 2);
            Expect.Equal(4, world.Orders.Count, "orders from code and the brain's are never answered");

            var ear = unit.Add(new OrderEar());
            ear.During = order =>
            {
                if (order.Name == Orders.Patrol)
                {
                    unit.OrderAt(Orders.Move, 0f, 0f);
                }
            };
            world.Orders.Clear();
            world.PlayerOrders(unit.Handle, Orders.Patrol, 300f, 0f);
            BrainCases.Steps(world, 2);
            Expect.Equal("point move", Given(world), "an order a reaction gives inside the event drops it too");
        }

        /// <summary>Sleep is off under full control, and given back as it was: on for a unit that could sleep, off for one that couldn't.</summary>
        public static void Sleep(FakeWorld world)
        {
            var creep = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            world.SetSleep(creep.Handle, true);
            var brain = creep.Add(new StepBrain { FullControl = true });
            Expect.True(!world.CanSleep(creep.Handle), "sleep is off under full control");
            brain.FullControl = false;
            Expect.True(world.CanSleep(creep.Handle), "and on again once given back");

            var footman = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var other = footman.Add(new StepBrain { FullControl = true });
            other.FullControl = false;
            Expect.True(!world.CanSleep(footman.Handle), "a unit that could not sleep still can't");
        }

        /// <summary>Given back by FullControl = false or by the brain ending, a unit left on Moonwell's hold gets Moonwell's own stop, and one whose last order was another doesn't; a dead or removed one gets none; a waiting answer is dropped, and nothing is answered after.</summary>
        public static void GiveBack(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var ear = unit.Add(new OrderEar());
            var brain = unit.Add(new StepBrain { FullControl = true });
            world.Acquire(unit.Handle);
            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 1);
            ear.Heard.Clear();
            world.Orders.Clear();
            brain.FullControl = false;
            Expect.Equal("immediate stop", Given(world), "a unit left on hold, by an answer at a step, is stopped");
            Expect.Equal(0, ear.Heard.Count, "as Moonwell's own order");
            Expect.Equal(0, Watches(world, unit), "and its picks are watched no more");
            world.Acquire(unit.Handle);
            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 2);
            Expect.Equal(1, world.Orders.Count, "then nothing is answered");

            brain.FullControl = true;
            world.PlayerOrders(unit.Handle, Orders.Move, 0f, 900f);
            brain.FullControl = false;
            BrainCases.Steps(world, 2);
            Expect.Equal(1, world.Orders.Count, "a waiting answer is dropped, and a unit not on Moonwell's hold gets no stop");

            var walker = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var walking = walker.Add(new StepBrain { FullControl = true });
            world.Acquire(walker.Handle);
            walking.OrderAt(Orders.Move, 500f, 0f);
            world.Orders.Clear();
            walking.End();
            Expect.Equal(0, world.Orders.Count, "the brain's own order took over from the hold: no stop");

            var held = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var holding = held.Add(new StepBrain { FullControl = true });
            world.Acquire(held.Handle);
            world.Orders.Clear();
            holding.End();
            Expect.Equal("immediate stop", Given(world), "the brain ending gives the unit back");

            var dying = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var mourning = dying.Add(new StepBrain { FullControl = true });
            world.Acquire(dying.Handle);
            dying.Kill();
            world.Orders.Clear();
            mourning.End();
            Expect.Equal(0, world.Orders.Count, "a dead unit gets no stop");

            var leaving = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            leaving.Add(new StepBrain { FullControl = true });
            world.Acquire(leaving.Handle);
            world.Orders.Clear();
            var gone = world.GoneCalls;
            leaving.Remove();
            Expect.Equal(0, world.Orders.Count, "a removed unit gets none");
            Expect.Equal(0, Watches(world, leaving), "and its watch ends");
            Expect.Equal(gone, world.GoneCalls, "and no call on a removed unit");
        }

        /// <summary>A brain that sends the same order to a place at every think gets it through after each answer, two orders not from code a step apart included: Moonwell's hold reads idle, which a brain that remembered its order would take for done.</summary>
        public static void NotStuckOnHold(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = new StepBrain { FullControl = true, ThinkEvery = 0.02f };
            brain.During = () => brain.OrderAt(Orders.Move, 1000f, 0f);
            unit.Add(brain);
            world.Orders.Clear();

            BrainCases.Steps(world, 2);
            Expect.Equal("point move", Given(world), "sent once, then dropped as a repeat");
            world.PlayerOrders(unit.Handle, Orders.Move, 0f, 900f);
            BrainCases.Steps(world, 1);
            world.PlayerOrders(unit.Handle, Orders.Move, 0f, 900f);
            BrainCases.Steps(world, 1);
            Expect.Equal("point move, immediate holdposition, point move, immediate holdposition, point move", Given(world), "after each answer the move reaches the game again");
            Expect.Equal("move", unit.CurrentOrder, "and the unit is on it, not left on hold");
        }

        /// <summary>Moonwell's calls that give the unit an order event are from code: MoveTo's stop, a slot move's 852005 and a change of owner's stop are never answered; an item's use, an instant order, isn't either, whoever gives it.</summary>
        public static void CodeCalls(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var ear = unit.Add(new OrderEar());
            var brain = unit.Add(new StepBrain { FullControl = true });
            var item = Item.Of(world.NewItem(ObjectTypes.FourCC("ebtf")))!;
            world.PutInSlot(unit.Handle, 0, item.Handle);
            world.Orders.Clear();

            brain.OrderAt(Orders.Move, 500f, 0f);
            unit.MoveTo(100f, 0f);
            Expect.Equal("", unit.CurrentOrder, "MoveTo stops the unit");
            brain.OrderAt(Orders.Move, 500f, 0f);
            Expect.True(unit.Give(item, 3), "the item moves to the fourth slot");
            Expect.Equal("move", unit.CurrentOrder, "and the move goes on");
            unit.Owner = Player.Get(1);
            Expect.Equal("", unit.CurrentOrder, "a change of owner stops the unit");
            world.PlayerOrdersId(unit.Handle, 852008, OrderKind.None);
            BrainCases.Steps(world, 2);
            Expect.Equal(6, ear.Heard.Count, "each fired its order event: the two moves, two stops, the slot move and the use");
            Expect.Equal("point move, point move", Given(world), "and none is answered with hold position");
        }

        /// <summary>A hold position from code is left as it is: a pick on it gives nothing, and given back the unit gets no stop and keeps its spot; after an order not from code a pick is held again.</summary>
        public static void CodeHold(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain { FullControl = true });
            brain.Order(Orders.HoldPosition);
            world.Orders.Clear();

            world.Acquire(unit.Handle);
            Expect.Equal(0, world.Orders.Count, "a unit the brain put on hold gives nothing when it picks a target");
            brain.FullControl = false;
            Expect.Equal(0, world.Orders.Count, "and given back, no stop moves it off its spot");

            brain.FullControl = true;
            unit.Order(Orders.HoldPosition);
            world.Orders.Clear();
            world.Acquire(unit.Handle);
            Expect.Equal(0, world.Orders.Count, "the unit's own Order is from code too");

            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 1);
            world.Orders.Clear();
            world.Acquire(unit.Handle);
            Expect.Equal("immediate holdposition", Given(world), "after an order not from code, a pick is held again");
        }

        /// <summary>An order not from code that a reaction refuses gets no hold: the order Moonwell gives back holds.</summary>
        public static void Refused(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain { FullControl = true });
            var ear = unit.Add(new OrderEar());
            ear.During = order =>
            {
                if (order.Name == Orders.Patrol)
                {
                    order.Cancel();
                }
            };
            brain.OrderAt(Orders.Move, 500f, 0f);
            world.Orders.Clear();

            world.PlayerOrders(unit.Handle, Orders.Patrol, 300f, 0f);
            Expect.Equal("point move", Given(world), "the reaction refuses it, and the brain's move is given back");
            BrainCases.Steps(world, 2);
            Expect.Equal("point move", Given(world), "no hold comes to undo that");
            Expect.Equal("move", unit.CurrentOrder, "and the unit goes on with the move");
        }

        /// <summary>Two brains on one unit: it is under full control while either has it, both are woken by an answer, and the last to give it up gives the unit back; its picks are watched once.</summary>
        public static void TwoBrains(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var first = unit.Add(new StepBrain { FullControl = true });
            var second = unit.Add(new StepBrain { FullControl = true });
            Expect.Equal(1, Watches(world, unit), "the unit's picks are watched once");
            world.Orders.Clear();
            world.Acquire(unit.Handle);
            first.FullControl = false;
            Expect.Equal("immediate holdposition", Given(world), "the first giving it up gives no stop while the other has it");
            world.Orders.Clear();

            world.PlayerOrders(unit.Handle, Orders.Move, 900f, 0f);
            BrainCases.Steps(world, 1);
            Expect.Equal("immediate holdposition", Given(world), "still under control while the other brain has it");
            Expect.Equal("1", string.Join(" ", first.Thought), "the unit's brains are woken");
            Expect.Equal("1", string.Join(" ", second.Thought), "both");

            world.Orders.Clear();
            second.End();
            Expect.Equal("immediate stop", Given(world), "the last one ending gives the unit back");
        }

        /// <summary>FullControl set in OnAdded, and set before the brain is added, takes effect once the brain is added; set off and on again, the unit's picks are still watched once.</summary>
        public static void SetEarly(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            unit.Add(new ControllingBrain());
            world.Orders.Clear();
            world.Acquire(unit.Handle);
            Expect.Equal("immediate holdposition", Given(world), "set in OnAdded");

            var other = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            world.SetSleep(other.Handle, true);
            var brain = new StepBrain { FullControl = true };
            Expect.True(world.CanSleep(other.Handle), "set before it is added, nothing happens yet");
            other.Add(brain);
            world.Orders.Clear();
            world.Acquire(other.Handle);
            Expect.Equal("immediate holdposition", Given(world), "and it takes effect when added");

            brain.FullControl = false;
            brain.FullControl = true;
            Expect.Equal(1, Watches(world, other), "set off and on again, the unit's picks are watched once");
        }
    }

    /// <summary>A unit component that notes the name of each order its unit's reactions hear, then runs the action a case gives it.</summary>
    public sealed class OrderEar : UnitComponent
    {
        /// <summary>The names of the orders heard, in order.</summary>
        public List<string> Heard = new List<string>();

        /// <summary>Run after the name is noted; null for nothing.</summary>
        public Action<Order>? During;

        public override void OnOrdered(Order order)
        {
            Heard.Add(order.Name);
            During?.Invoke(order);
        }
    }

    /// <summary>A unit brain that takes full control in its OnAdded.</summary>
    public sealed class ControllingBrain : UnitBrain
    {
        protected override void OnAdded()
        {
            FullControl = true;
        }

        protected override void Think()
        {
        }
    }
}
