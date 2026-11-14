using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.AI
{
    /// <summary>
    /// A unit under full control (decisions 330 to 332), while any of its brains has FullControl. A target it picks by
    /// itself with no order is answered with hold position inside the acquire event. Any other order not from code, the
    /// game's, an AI script's or a person's, is answered with hold position at the loop's next step, not inside its event,
    /// which would not hold, and for a computer's attack back closes the game (game facts, 11-14). Its sleep is off while
    /// under control.
    /// </summary>
    internal sealed class Control
    {
        /// <summary>The game's id of holdposition.</summary>
        private const int HoldId = 851993;

        /// <summary>The game the units below are in; a runtime reset makes another, and they are forgotten.</summary>
        private static Game? game;

        /// <summary>Each unit under full control; a lookup only.</summary>
        private static readonly Dictionary<Unit, Control> units = new Dictionary<Unit, Control>();

        /// <summary>The units with an answer waiting for the loop's next step, in the order their orders came.</summary>
        private static readonly List<Control> waiting = new List<Control>();

        /// <summary>The unit.</summary>
        private readonly Unit unit;

        /// <summary>Whether the unit could sleep when control began, so give-back lets it again.</summary>
        private readonly bool couldSleep;

        /// <summary>How many of the unit's brains have FullControl.</summary>
        private int brains = 1;

        /// <summary>Whether Moonwell's hold is the last order the unit was given.</summary>
        private bool held;

        /// <summary>Whether an order not from code waits for its answer.</summary>
        private bool answering;

        /// <summary>Whether the unit's last order was hold position from code, so it already holds and a pick needs no answer.</summary>
        private bool codeHold;

        private Control(Unit unit)
        {
            this.unit = unit;
            couldSleep = Runtime.World.CanSleep(unit.Handle);
            Runtime.World.SetSleep(unit.Handle, false);
        }

        /// <summary>Counts the brain in or out of its unit's control as its FullControl and its life say: in while FullControl is set and the brain is added and has not begun to end.</summary>
        internal static void Steer(UnitBrain brain)
        {
            var want = brain.fullControl && brain.added && !brain.ending && !brain.HasEnded;
            if (want == brain.controlling)
            {
                return;
            }

            brain.controlling = want;
            if (want)
            {
                Take(brain.Unit);
            }
            else
            {
                Give(brain.Unit);
            }
        }

        /// <summary>
        /// Gives the waiting answers: hold position as Moonwell's own order to each unit still under control and alive, then its
        /// brains forget the order they last sent and are woken. Moonwell's hold reads 0, as idle, so a brain that remembered
        /// its order would take it as done and drop it. The loop calls it at each step before the step's number goes up, so the
        /// wakes think in that step.
        /// </summary>
        internal static void Answer()
        {
            if (waiting.Count == 0)
            {
                return;
            }

            Fresh();
            for (var i = 0; i < waiting.Count; i++)
            {
                var control = waiting[i];
                if (!control.answering)
                {
                    continue;
                }

                control.answering = false;
                var unit = control.unit;
                if (unit.IsGone || unit.ending || !unit.IsAlive)
                {
                    continue;
                }

                control.Hold();
                var components = unit.components;
                for (var k = 0; k < components.Count; k++)
                {
                    var brain = components[k] as UnitBrain;
                    if (brain != null)
                    {
                        brain.sentOrder = null;
                        brain.retried = false;
                        brain.Wake();
                    }
                }
            }

            waiting.Clear();
        }

        /// <summary>A brain takes the unit: the first stops its sleep, watches its picks and hears its orders.</summary>
        private static void Take(Unit unit)
        {
            Fresh();
            if (units.TryGetValue(unit, out var control))
            {
                control.brains++;
                return;
            }

            units[unit] = new Control(unit);
            Events.AcquireListener = Acquired;
            Events.OrderListener = Ordered;
            Events.Listen(GameEvent.Order);
            Runtime.World.WatchAcquire(unit.Handle);
        }

        /// <summary>A brain gives the unit back: the last stops the answers and the watch of its picks, stops a unit Moonwell left on hold, so the game picks up at once, unless it is dead or leaving, and lets it sleep again if it could.</summary>
        private static void Give(Unit unit)
        {
            Fresh();
            if (!units.TryGetValue(unit, out var control))
            {
                return;
            }

            control.brains--;
            if (control.brains > 0)
            {
                return;
            }

            units.Remove(unit);
            control.answering = false;
            Runtime.World.UnwatchAcquire(unit.Handle);
            if (unit.IsGone)
            {
                return;
            }

            if (control.held && !unit.ending && unit.IsAlive)
            {
                Inbound.GiveOwn(unit, () => unit.Order(Orders.Stop));
            }

            if (control.couldSleep)
            {
                Runtime.World.SetSleep(unit.Handle, true);
            }
        }

        /// <summary>A unit picks a target by itself: held at once when under control with no order, idle or on a hold not from code, both of which read 0. A unit code put on hold holds already.</summary>
        private static void Acquired(Unit unit)
        {
            if (units.TryGetValue(unit, out var control) && !control.codeHold && Runtime.World.CurrentOrderOf(unit.Handle) == 0)
            {
                control.Hold();
            }
        }

        /// <summary>A unit under control is given an order: it is no longer on Moonwell's hold. One from code drops a waiting answer and notes whether it is hold position; any other waits for one.</summary>
        private static void Ordered(Unit unit, int id, bool fromCode)
        {
            if (!units.TryGetValue(unit, out var control))
            {
                return;
            }

            control.held = false;
            control.codeHold = fromCode && id == HoldId;
            if (fromCode)
            {
                control.answering = false;
            }
            else if (!control.answering)
            {
                control.answering = true;
                waiting.Add(control);
            }
        }

        /// <summary>Forgets the units of an earlier game.</summary>
        private static void Fresh()
        {
            if (game != Game.Instance)
            {
                game = Game.Instance;
                units.Clear();
                waiting.Clear();
            }
        }

        /// <summary>Gives the unit hold position as Moonwell's own order, which no reaction hears and nothing answers.</summary>
        private void Hold()
        {
            if (Inbound.GiveOwn(unit, () => unit.Order(Orders.HoldPosition)))
            {
                held = true;
            }
        }
    }
}
