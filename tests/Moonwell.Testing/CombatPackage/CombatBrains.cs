using Moonwell.AI;
using Moonwell.AI.Combat;
using static Moonwell.AI.Nodes;

namespace Moonwell.Testing
{
    /// <summary>A unit brain with a fighter, as a map's own brain has one: it never thinks by itself, notes its attackers through the fighter, and counts its thinks.</summary>
    public sealed class CombatBrain : UnitBrain
    {
        /// <summary>The brain's fighter.</summary>
        public readonly Fighter Fighter;

        /// <summary>How many times it thought.</summary>
        public int Thinks;

        public CombatBrain()
        {
            Fighter = new Fighter(this);
        }

        public override void OnDamaged(Damage damage)
        {
            Fighter.Hurt(damage);
        }

        protected override void Think()
        {
            Thinks++;
        }
    }

    /// <summary>A lane walker with a tree of the map's own, which counts its passes.</summary>
    public sealed class OwnTreeWalker : LaneWalker
    {
        /// <summary>How many passes its own root ran.</summary>
        public int Passes;

        protected override Node Root()
        {
            return Do(() =>
            {
                Passes++;
                return Status.Running;
            });
        }
    }
}
