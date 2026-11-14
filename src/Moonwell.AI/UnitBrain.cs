namespace Moonwell.AI
{
    /// <summary>
    /// A unit's brain: a component whose <c>Think</c> runs every <c>ThinkEvery</c> seconds, when woken, or both, on the
    /// package's loop of 0.02 s steps. It is added like any component (<c>unit.Add(new Guard())</c>), hears the unit's
    /// reactions, and ends with the unit. Its blackboard's chain goes on to the blackboard of the unit's owner at each read,
    /// so after <c>unit.Owner = other</c> it reads the new owner's, keeping its own facts. Its orders reach the game only
    /// when they are new.
    /// </summary>
    public abstract class UnitBrain : UnitComponent
    {
        /// <summary>An order with no place and no target.</summary>
        private const int Plain = 0;

        /// <summary>An order to a place.</summary>
        private const int AtPlace = 1;

        /// <summary>An order on a widget.</summary>
        private const int OnWidget = 2;

        /// <summary>How near a place is the same place, squared: 32.</summary>
        private const float SamePlace = 32f * 32f;

        /// <summary>How near the unit is to have reached a place, squared: 64.</summary>
        private const float Reached = 64f * 64f;

        /// <summary>The brain's tick, wakes and blackboard. Internal, not private, for the reason Scope gives.</summary>
        internal readonly Mind mind;

        /// <summary>The order string last sent; null for none.</summary>
        internal string? sentOrder;

        /// <summary>Whether the last order was plain, to a place or on a widget.</summary>
        internal int sentKind;

        /// <summary>The place of the last order to a place.</summary>
        internal float sentX;

        /// <summary>The place of the last order to a place.</summary>
        internal float sentY;

        /// <summary>The target of the last order on a widget.</summary>
        internal Widget? sentTarget;

        /// <summary>The unit's CurrentOrder read just after the last order was sent.</summary>
        internal string sentCurrent = "";

        /// <summary>The unit's owner when the last order was sent.</summary>
        internal Player? sentOwner;

        /// <summary>Whether the last order to a place was sent again to the unit idle short of it, at retryX and retryY.</summary>
        internal bool retried;

        /// <summary>Where the unit stood when the last order to a place was sent again.</summary>
        internal float retryX;

        /// <summary>Where the unit stood when the last order to a place was sent again.</summary>
        internal float retryY;

        /// <summary>FullControl as set.</summary>
        internal bool fullControl;

        /// <summary>Whether the brain counts toward its unit's full control.</summary>
        internal bool controlling;

        /// <summary>Makes the brain; it starts thinking once added.</summary>
        protected UnitBrain()
        {
            mind = new Mind(this, Think);
        }

        /// <summary>The seconds between thinks, rounded to whole milliseconds, then to whole steps of 0.02 s, a half step up (a quarter second is 13 steps, 0.03 s is 2), and at least one step; 0, the default, and it thinks only when woken. Set it at any time, in OnAdded too: a change thinks within the new tick from now, then at every tick, and brains started together spread their first thinks over their tick.</summary>
        [LocalSafe]
        public float ThinkEvery
        {
            get { return mind.ThinkEvery; }
            set { mind.SetEvery(value); }
        }

        /// <summary>The brain's own blackboard, the first of its chain: then the blackboard of the unit's owner now, then the game's. It ends with the brain.</summary>
        [LocalSafe]
        public Blackboard Blackboard => mind.Board;

        /// <summary>
        /// Whether the brain has full control of its unit; false, the default, leaves the game's own unit AI on, as a unit
        /// picks targets, hits back and goes home by itself. True stops it: a target the unit picks by itself with no order
        /// is answered at once with hold position, and every order not from code (the game's, an AI script's, a person's
        /// click) with hold position at the next step, waking the unit's brains; the unit doesn't sleep. An enemy right beside
        /// it is still hit, as on hold position, and an order from code always goes through. Set it at any time, in OnAdded
        /// too; the unit is under full control while any of its brains has it. Set false, or the last such brain ending,
        /// gives the unit back: it sleeps again if it could, and one left on Moonwell's hold gets a stop, so the game picks
        /// up at once.
        /// </summary>
        public bool FullControl
        {
            get
            {
                return fullControl;
            }

            set
            {
                fullControl = value;
                Control.Steer(this);
            }
        }

        /// <summary>Has the brain think at the next step, within 0.02 s: once in that step however many wakes come, a tick due then included. A wake from inside a step goes to the step after, unless the brain still waits to think from a wake in this one.</summary>
        public void Wake()
        {
            mind.Wake();
        }

        /// <summary>The value of a fact, read from the brain's blackboard up its chain, as <c>Blackboard.Read</c>.</summary>
        public T Read<T>(Fact<T> fact)
        {
            return mind.Board.Read(fact);
        }

        /// <summary>The seconds since the fact a read would find was written or worked out, as <c>Blackboard.Age</c>; <c>float.MaxValue</c> when there is none.</summary>
        public float Age<T>(Fact<T> fact)
        {
            return mind.Board.Age(fact);
        }

        /// <summary>
        /// Gives the unit an order with no place and no target, as the unit's own <c>Order</c>, unless it repeats the last one
        /// the brain sent: then nothing reaches the game and it gives true. A repeat is dropped while the unit's
        /// <c>CurrentOrder</c> is still the order it read just after that one (not idle), or while the unit is idle; once the
        /// unit's owner has changed, it is sent again.
        /// </summary>
        public bool Order(string order)
        {
            return Send(order, Plain, 0f, 0f, null);
        }

        /// <summary>
        /// Gives the unit an order to a place, as the unit's own <c>OrderAt</c>, unless it repeats the last one the brain sent:
        /// the same order to a place within 32 of it. A repeat is dropped, giving true, while the unit's <c>CurrentOrder</c> is
        /// still the order it read just after that one (not idle), or while the unit is idle within 64 of that place. A unit idle
        /// short of it gets the repeat once; idle again within 32 of where it stood then, the repeat is dropped. Once the unit's
        /// owner has changed, it is sent again.
        /// </summary>
        public bool OrderAt(string order, float x, float y)
        {
            return Send(order, AtPlace, x, y, null);
        }

        /// <summary>
        /// Gives the unit an order on a unit, an item or a destructable, as the unit's own <c>OrderOn</c>, unless it repeats the
        /// last one the brain sent: the same order on the same target. A repeat is dropped, giving true, while the unit's
        /// <c>CurrentOrder</c> is still the order it read just after that one (not idle), or while the unit is idle and the
        /// target is dead or gone; once the unit's owner has changed, it is sent again.
        /// </summary>
        public bool OrderOn(string order, Widget target)
        {
            return Send(order, OnWidget, 0f, 0f, target);
        }

        /// <summary>The brain's work, run at its tick and when woken. One that throws is reported, and the other brains still think.</summary>
        protected abstract void Think();

        /// <summary>Runs OnAdded, then starts the brain, so OnAdded can set ThinkEvery first.</summary>
        internal override void CallAdded()
        {
            mind.Board.unit = Unit;
            try
            {
                base.CallAdded();
            }
            finally
            {
                mind.Start();
                Control.Steer(this);
            }
        }

        /// <summary>The brain has ended: so has its blackboard, the loop counts it out, and it gives back its unit's full control.</summary>
        internal override void Finish()
        {
            base.Finish();
            mind.End();
            Control.Steer(this);
        }

        /// <summary>
        /// Sends an order through the unit's own call unless it repeats the last and the unit is still on it or has done it,
        /// and remembers one the game took; one it refused is forgotten, so the next is sent. An idle unit is asked whether it
        /// has done it even when it read idle just after the order, as when the order was cancelled in its own event. A unit
        /// idle short of its place gets the repeat once: idle again within 32 of where it stood then, it has done it, as a
        /// crowd jammed at one place would otherwise be ordered again at every think. An order that is no repeat, or a change
        /// of owner, forgets that spot.
        /// </summary>
        private bool Send(string order, int kind, float x, float y, Widget? target)
        {
            var unit = Unit;
            var repeat = sentOrder == order && sentKind == kind && sentTarget == target
                && (kind != AtPlace || Squared(x - sentX, y - sentY) <= SamePlace)
                && unit.Owner == sentOwner;
            var retry = false;
            var standX = 0f;
            var standY = 0f;
            if (repeat)
            {
                var current = unit.CurrentOrder;
                if (current != "" && current == sentCurrent)
                {
                    return true;
                }

                if (current == "")
                {
                    if (kind == AtPlace)
                    {
                        standX = unit.X;
                        standY = unit.Y;
                        if (Squared(standX - sentX, standY - sentY) <= Reached || (retried && Squared(standX - retryX, standY - retryY) <= SamePlace))
                        {
                            return true;
                        }

                        retry = true;
                    }
                    else if (kind == Plain || Gone(target!))
                    {
                        return true;
                    }
                }
            }

            bool taken;
            if (kind == AtPlace)
            {
                taken = unit.OrderAt(order, x, y);
            }
            else if (kind == OnWidget)
            {
                taken = unit.OrderOn(order, target!);
            }
            else
            {
                taken = unit.Order(order);
            }

            if (!taken)
            {
                sentOrder = null;
                retried = false;
                return false;
            }

            if (retry)
            {
                retried = true;
                retryX = standX;
                retryY = standY;
            }
            else if (!repeat)
            {
                retried = false;
            }

            sentOrder = order;
            sentKind = kind;
            sentX = x;
            sentY = y;
            sentTarget = target;
            sentCurrent = unit.CurrentOrder;
            sentOwner = unit.Owner;
            return true;
        }

        /// <summary>Whether a target is dead or gone.</summary>
        private static bool Gone(Widget target)
        {
            var targetUnit = target as Unit;
            if (targetUnit != null)
            {
                return !targetUnit.IsAlive;
            }

            var destructable = target as Destructable;
            if (destructable != null)
            {
                return !destructable.IsAlive;
            }

            return target.IsGone;
        }

        private static float Squared(float dx, float dy)
        {
            return dx * dx + dy * dy;
        }
    }
}
