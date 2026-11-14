namespace Moonwell.AI.Combat
{
    /// <summary>
    /// Fights the fighter's target: at each pass the first of its spells whose aim has someone, whose condition holds and
    /// that is ready is cast, then watched as <c>Cast</c> watches it, Running while the unit's order id is the one read just
    /// after; otherwise, and once the cast is over, it attacks the target. Every order goes through the brain, so a repeat
    /// the unit carries out never reaches the game. Failed with no target, for a dead or removed unit, and when the attack
    /// is refused.
    /// </summary>
    internal sealed class FightNode : Node
    {
        /// <summary>The fighter whose target and spells it reads.</summary>
        private readonly Fighter fighter;

        /// <summary>Whether a cast it gave is watched.</summary>
        private bool casting;

        /// <summary>The unit's order id read just after the cast.</summary>
        private int castId;

        /// <summary>A fight of a fighter.</summary>
        internal FightNode(Fighter fighter)
        {
            this.fighter = fighter;
        }

        /// <summary>Starts with no cast watched.</summary>
        protected override void Start()
        {
            casting = false;
        }

        /// <summary>Watches a cast under way, else casts the first ready spell, else attacks.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            var target = fighter.Target;
            if (target == null)
            {
                return Status.Failed;
            }

            if (casting)
            {
                if (Runtime.World.CurrentOrderOf(unit.Handle) == castId)
                {
                    return Status.Running;
                }

                casting = false;
            }

            if (CastFirstReady(unit, target))
            {
                return Status.Running;
            }

            return OrderOn(Orders.Attack, target) ? Status.Running : Status.Failed;
        }

        /// <summary>Casts the first spell whose aim has someone, whose condition holds and that is ready, going on to the next when the game refuses one; true when the unit carries a cast out, false when none was cast or it ended at once.</summary>
        private bool CastFirstReady(Unit unit, Unit target)
        {
            var spells = fighter.Spells;
            for (var i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                Unit? aim = target;
                if (spell.aim == Spell.NoTarget)
                {
                    if (spell.whenFighter != null && !spell.whenFighter(fighter))
                    {
                        continue;
                    }
                }
                else if (spell.aim == Spell.OnAlly)
                {
                    aim = fighter.MostHurtAlly(spell.whenUnit);
                    if (aim == null)
                    {
                        continue;
                    }
                }
                else if (spell.whenUnit != null && !spell.whenUnit(target))
                {
                    continue;
                }

                if (!spell.Ready(unit) || !Give(spell, aim))
                {
                    continue;
                }

                castId = Runtime.World.CurrentOrderOf(unit.Handle);
                casting = castId != 0;
                return casting;
            }

            return false;
        }

        /// <summary>Gives the cast through the brain: on the aim, at its place, or with no target; whether the game took it.</summary>
        private bool Give(Spell spell, Unit aim)
        {
            if (spell.aim == Spell.NoTarget)
            {
                return Order(spell.Order);
            }

            if (spell.aim == Spell.AtPlace)
            {
                return OrderAt(spell.Order, aim.X, aim.Y);
            }

            return OrderOn(spell.Order, aim);
        }
    }
}
