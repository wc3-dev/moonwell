namespace Moonwell.Testing
{
    /// <summary>A unit type that logs each order it hears as "unit name" and keeps the last.</summary>
    public sealed class OrderedUnit : Unit
    {
        /// <summary>The last order heard; null before one.</summary>
        public Order? Heard;

        /// <summary>Logs the order.</summary>
        public override void OnOrdered(Order order)
        {
            Heard = order;
            TestLog.Lines.Add("unit " + order.Name);
        }
    }

    /// <summary>A unit component that logs each order as "component name".</summary>
    public sealed class OrderedComponent : UnitComponent
    {
        /// <summary>Logs the order.</summary>
        public override void OnOrdered(Order order)
        {
            TestLog.Lines.Add("component " + order.Name);
        }
    }

    /// <summary>A game component that logs each order of a unit as "game name" and keeps the last.</summary>
    public sealed class OrderedGame : GameComponent
    {
        /// <summary>The last order heard; null before one.</summary>
        public Order? Heard;

        /// <summary>Logs the order.</summary>
        public override void OnUnitOrdered(Order order)
        {
            Heard = order;
            TestLog.Lines.Add("game " + order.Name);
        }
    }

    /// <summary>A player component that logs each order of its player's units as "player name".</summary>
    public sealed class OrderedPlayer : PlayerComponent
    {
        /// <summary>Logs the order.</summary>
        public override void OnUnitOrdered(Order order)
        {
            TestLog.Lines.Add("player " + order.Name);
        }
    }

    /// <summary>A game component that cancels every move to a place east of 0, and logs "cancel".</summary>
    public sealed class CancelsMovesEast : GameComponent
    {
        /// <summary>Cancels a move east.</summary>
        public override void OnUnitOrdered(Order order)
        {
            if (order.Name == Orders.Move && order.X > 0f)
            {
                TestLog.Lines.Add("cancel");
                order.Cancel();
            }
        }
    }

    /// <summary>A game component that replaces every move east of 0 with a move to -100, 0.</summary>
    public sealed class ReplacesMovesEast : GameComponent
    {
        /// <summary>Orders a move west instead.</summary>
        public override void OnUnitOrdered(Order order)
        {
            if (order.Name == Orders.Move && order.X > 0f)
            {
                order.Unit.OrderAt(Orders.Move, -100f, 0f);
            }
        }
    }

    /// <summary>A game component that stops the unit of every move east of 0, inside the reaction, and cancels it when told to.</summary>
    public sealed class StopsMovesEast : GameComponent
    {
        /// <summary>Whether it cancels the move too.</summary>
        private readonly bool cancel;

        /// <summary>Makes one that stops, and cancels when cancel is true.</summary>
        public StopsMovesEast(bool cancel)
        {
            this.cancel = cancel;
        }

        /// <summary>Stops the unit, and cancels the move.</summary>
        public override void OnUnitOrdered(Order order)
        {
            if (order.Name == Orders.Move && order.X > 0f)
            {
                if (cancel)
                {
                    order.Cancel();
                }

                order.Unit.Stop();
            }
        }
    }

    /// <summary>A game component that logs whether an order was cancelled as "cancelled true".</summary>
    public sealed class ReadsCancelled : GameComponent
    {
        /// <summary>Logs IsCancelled.</summary>
        public override void OnUnitOrdered(Order order)
        {
            TestLog.Lines.Add("cancelled " + (order.IsCancelled ? "true" : "false"));
        }
    }

    /// <summary>A unit type that removes itself when ordered.</summary>
    public sealed class RemovingUnit : Unit
    {
        /// <summary>Logs and removes the unit.</summary>
        public override void OnOrdered(Order order)
        {
            TestLog.Lines.Add("removing");
            Remove();
        }
    }

    /// <summary>A unit type that gives itself a new move in every order reaction, so its orders nest without end.</summary>
    public sealed class NestingUnit : Unit
    {
        /// <summary>How many reactions ran.</summary>
        public int Heard;

        /// <summary>Counts, and orders a move.</summary>
        public override void OnOrdered(Order order)
        {
            Heard++;
            OrderAt(Orders.Move, Heard, 0f);
        }
    }
}
