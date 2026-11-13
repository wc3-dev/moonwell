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

        /// <summary>Makes the brain; it starts thinking once added.</summary>
        protected UnitBrain()
        {
            mind = new Mind(this, Think);
        }

        /// <summary>The seconds between thinks, rounded to whole steps of 0.02 s, a half up (a quarter second is 13 steps), and at least one step; 0, the default, and it thinks only when woken. Set it at any time, in OnAdded too: a change thinks within the new tick from now, then at every tick, and brains started together spread their first thinks over their tick.</summary>
        public float ThinkEvery
        {
            get { return mind.ThinkEvery; }
            set { mind.SetEvery(value); }
        }

        /// <summary>The brain's own blackboard, the first of its chain: then the blackboard of the unit's owner now, then the game's. It ends with the brain.</summary>
        public Blackboard Blackboard => mind.Board;

        /// <summary>Has the brain think at the next step, within 0.02 s: once in that step however many wakes come, a tick due then included. A wake from inside a step goes to the step after.</summary>
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
        /// <c>CurrentOrder</c> is still what it read just after that order, or while the unit is idle; once the unit's owner
        /// has changed, it is sent again.
        /// </summary>
        public bool Order(string order)
        {
            return Send(order, Plain, 0f, 0f, null);
        }

        /// <summary>
        /// Gives the unit an order to a place, as the unit's own <c>OrderAt</c>, unless it repeats the last one the brain sent:
        /// the same order to a place within 32 of it. A repeat is dropped, giving true, while the unit's <c>CurrentOrder</c> is
        /// still what it read just after that order, or while the unit is idle within 64 of that place; once the unit's owner
        /// has changed, it is sent again.
        /// </summary>
        public bool OrderAt(string order, float x, float y)
        {
            return Send(order, AtPlace, x, y, null);
        }

        /// <summary>
        /// Gives the unit an order on a unit, an item or a destructable, as the unit's own <c>OrderOn</c>, unless it repeats the
        /// last one the brain sent: the same order on the same target. A repeat is dropped, giving true, while the unit's
        /// <c>CurrentOrder</c> is still what it read just after that order, or while the unit is idle and the target is dead or
        /// gone; once the unit's owner has changed, it is sent again.
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
            }
        }

        /// <summary>The brain has ended: so has its blackboard, and the loop counts it out.</summary>
        internal override void Finish()
        {
            base.Finish();
            mind.End();
        }

        /// <summary>Sends an order through the unit's own call unless it repeats the last, and remembers one the game took; one it refused is forgotten, so the next is sent.</summary>
        private bool Send(string order, int kind, float x, float y, Widget? target)
        {
            var unit = Unit;
            if (Repeats(unit, order, kind, x, y, target))
            {
                return true;
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
                return false;
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

        /// <summary>Whether an order repeats the last one sent and the unit is still on it or has done it.</summary>
        private bool Repeats(Unit unit, string order, int kind, float x, float y, Widget? target)
        {
            if (sentOrder != order || sentKind != kind || sentTarget != target)
            {
                return false;
            }

            if (kind == AtPlace && Squared(x - sentX, y - sentY) > SamePlace)
            {
                return false;
            }

            if (unit.Owner != sentOwner)
            {
                return false;
            }

            var current = unit.CurrentOrder;
            if (current == sentCurrent)
            {
                return true;
            }

            return current == "" && Done(unit);
        }

        /// <summary>Whether an idle unit has done the last order: reached its place, seen its target dead or gone, or had neither.</summary>
        private bool Done(Unit unit)
        {
            if (sentKind == AtPlace)
            {
                return Squared(unit.X - sentX, unit.Y - sentY) <= Reached;
            }

            if (sentKind == OnWidget)
            {
                return Gone(sentTarget!);
            }

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
