namespace Moonwell.AI
{
    /// <summary>
    /// A built-in node that gives the unit one order at its first pass, then watches the unit's current order id read just
    /// after it: Running while the unit still reads that id, then Succeeded; Succeeded at once when it reads 0. Failed when
    /// the order is not given, or the unit is dead or removed. Succeeded means the unit stopped carrying the order out, not
    /// that it took effect: a stun, or another order, reads the same.
    /// </summary>
    internal abstract class OrderWatch : Node
    {
        /// <summary>Whether the order was given at this run's first pass.</summary>
        private bool given;

        /// <summary>The unit's current order id read just after the order was given.</summary>
        private int watched;

        /// <summary>Gives the order at the first pass; false when it was not given, failing the node.</summary>
        protected abstract bool Give(Unit unit);

        /// <summary>Starts with no order given.</summary>
        protected override void Start()
        {
            given = false;
        }

        /// <summary>Gives the order at the first pass, then watches the unit's current order id.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            if (given)
            {
                return Runtime.World.CurrentOrderOf(unit.Handle) == watched ? Status.Running : Status.Succeeded;
            }

            if (!Give(unit))
            {
                return Status.Failed;
            }

            given = true;
            watched = Runtime.World.CurrentOrderOf(unit.Handle);
            return watched == 0 ? Status.Succeeded : Status.Running;
        }
    }
}
