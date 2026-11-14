using static Moonwell.AI.Nodes;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// The combat nodes a lane walker's tree is built of, for a map's own tree too: <c>using static
    /// Moonwell.AI.Combat.CombatNodes;</c>. Each makes a new node of a fighter, which the fighter's own brain runs.
    /// </summary>
    public static class CombatNodes
    {
        /// <summary>
        /// Fights the fighter's target, read at every pass: casts the first of its spells whose aim has someone, whose
        /// condition holds and that is ready, Running while the unit carries the cast out, as <c>Cast</c> watches it, and
        /// wakes the brain when the cast ends, as the game then leaves the unit idle; then it goes back to attacking the
        /// target. A spell the game refuses gives way to the next, and the attack the unit carries out is left alone. Failed
        /// with no target, for a dead or removed unit, and when the attack is refused. Put it under
        /// <c>When(fighter.Meets, ...)</c>, whose check picks the target.
        /// </summary>
        public static Node Fight(Fighter fighter)
        {
            return new FightNode(fighter);
        }

        /// <summary>
        /// Pulls the fighter back with move orders, which never fight on the way: to its PullBack's <c>To</c>, else back
        /// along its route point by point to the start, from the point before the one nearest the unit, else to where its
        /// fight started. There, or with nowhere to go, it stands on hold position. Running while the unit lives; Failed for
        /// a dead or removed unit and a refused order. Put it under <c>When(fighter.ShouldPullBack, ...)</c>.
        /// </summary>
        public static Node PullBack(Fighter fighter)
        {
            return new PullBackNode(fighter);
        }

        /// <summary>
        /// Walks the fighter's route as <c>FollowRoute</c> does, with move orders, taking it up at the point after the one
        /// nearest the unit, so after a fight it goes on from where it is. With no route, it walks back to where its last
        /// fight started, then fails, as there is nothing to walk.
        /// </summary>
        public static Node WalkLane(Fighter fighter)
        {
            return Select(
                When(fighter.ReturnsToSpot, MoveTo(() => fighter.spotX, () => fighter.spotY)),
                FollowRoute(() => fighter.Route));
        }
    }
}
