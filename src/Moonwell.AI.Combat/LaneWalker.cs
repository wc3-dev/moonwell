using System.Collections.Generic;
using static Moonwell.AI.Nodes;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// A ready unit brain that walks a route and fights what it meets, then goes on: <c>unit.Add(new LaneWalker(route) {
    /// Targets = { Target.Weakest }, Spells = { ... } })</c>. Its settings are its fighter's. It thinks every 0.25 s with
    /// full control, set in its OnAdded, so a map's own OnAdded changes them after <c>base.OnAdded()</c>. Its tree, from
    /// <c>Root</c>, is built at its first think: pull back while it should, else fight while it meets an enemy, else walk
    /// its route. A map inherits it and overrides <c>Root</c> for a tree of its own from the same nodes.
    /// </summary>
    public class LaneWalker : UnitBrain
    {
        /// <summary>The brain's tree; null until its first think. Internal, not private, for the reason Scope gives.</summary>
        internal Tree? tree;

        /// <summary>A lane walker on a route; with none, it stands and fights where it is.</summary>
        public LaneWalker(Route? route = null)
        {
            Fighter = new Fighter(this) { Route = route };
        }

        /// <summary>The brain's fighter, whose settings the brain's are and whose target, counts and pulling back a map reads.</summary>
        [LocalSafe]
        public Fighter Fighter { get; }

        /// <summary>The route it walks, as the fighter's <c>Route</c>.</summary>
        [LocalSafe]
        public Route? Route
        {
            get { return Fighter.Route; }
            set { Fighter.Route = value; }
        }

        /// <summary>How far it meets enemies, as the fighter's <c>MeetRange</c>: 0, the default, takes the unit's acquisition range.</summary>
        [LocalSafe]
        public float MeetRange
        {
            get { return Fighter.MeetRange; }
            set { Fighter.MeetRange = value; }
        }

        /// <summary>How far from where its fight started an enemy is fought, as the fighter's <c>Leash</c>: 2,100, the default.</summary>
        [LocalSafe]
        public float Leash
        {
            get { return Fighter.Leash; }
            set { Fighter.Leash = value; }
        }

        /// <summary>The rules its target is picked by, the fighter's <c>Targets</c>; with none, the nearest.</summary>
        [LocalSafe]
        public List<Target> Targets => Fighter.Targets;

        /// <summary>The spells it casts while it fights, the fighter's <c>Spells</c>.</summary>
        [LocalSafe]
        public List<Spell> Spells => Fighter.Spells;

        /// <summary>When it pulls back and comes back, the fighter's <c>PullBack</c>, set in braces: <c>PullBack = { Below = 0.25f }</c>.</summary>
        [LocalSafe]
        public PullBack PullBack
        {
            get { return Fighter.PullBack; }
            set { Fighter.PullBack = value; }
        }

        /// <summary>Notes an attacker for the fighter, which wakes the brain when it is new. A map's own override calls <c>base.OnDamaged(damage)</c>.</summary>
        public override void OnDamaged(Damage damage)
        {
            Fighter.Hurt(damage);
        }

        /// <summary>Thinks every 0.25 s with full control of its unit.</summary>
        protected override void OnAdded()
        {
            ThinkEvery = 0.25f;
            FullControl = true;
        }

        /// <summary>Builds the tree at the first think, then runs it.</summary>
        protected override void Think()
        {
            if (tree == null)
            {
                tree = new Tree(this, Root());
            }

            tree.Run();
        }

        /// <summary>The root of the brain's tree, asked for once, at its first think: <c>Select(When(Fighter.ShouldPullBack, PullBack(Fighter)), When(Fighter.Meets, Fight(Fighter)), WalkLane(Fighter))</c>.</summary>
        protected virtual Node Root()
        {
            return Select(
                When(Fighter.ShouldPullBack, CombatNodes.PullBack(Fighter)),
                When(Fighter.Meets, CombatNodes.Fight(Fighter)),
                CombatNodes.WalkLane(Fighter));
        }
    }
}
