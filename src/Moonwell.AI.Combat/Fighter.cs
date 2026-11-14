using System;
using System.Collections.Generic;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// One fighter's settings, and what it found at its last check, for a unit brain: a <c>LaneWalker</c> has one, and a
    /// map's own brain makes one, <c>new Fighter(this)</c>, for the combat nodes of its tree. A check runs once an instant,
    /// from <c>Meets</c> or <c>ShouldPullBack</c>: it finds the enemies the fighter meets and picks its target. It meets a
    /// living enemy unit, structures too, that its unit's owner sees, within its meet range or one that damaged its unit in
    /// the last 3 s however far, and none farther than its leash from where its fight started.
    /// </summary>
    public sealed class Fighter
    {
        /// <summary>The seconds an attacker is remembered after its last hit: 3.</summary>
        internal const float Memory = 3f;

        /// <summary>How near a place the unit is to be back at it, squared: 64, as the unit-action nodes reach a place.</summary>
        internal const float Reached = 64f * 64f;

        /// <summary>The distance below which Nearest counts a distance as this, squared: 16.</summary>
        private const float NearestFloor = 16f * 16f;

        /// <summary>The damage a second below which MostDangerous counts it as this: 1.</summary>
        private const float DangerFloor = 1f;

        /// <summary>How much more than the current target's score another needs to take over, as a factor: more than a tenth.</summary>
        private const float Keep = 1.1f;

        /// <summary>The brain whose unit fights.</summary>
        private readonly UnitBrain brain;

        /// <summary>The units that damaged the fighter's unit, as Hurt noted them; those older than Memory are forgotten at a check.</summary>
        private readonly List<Unit> attackers = new List<Unit>();

        /// <summary>When each of the attackers last hit, in the game's seconds.</summary>
        private readonly List<float> hitAt = new List<float>();

        /// <summary>The enemies met at the last check.</summary>
        private readonly List<Unit> met = new List<Unit>();

        /// <summary>The allies in meet range at the last check, the fighter's unit first, structures left out.</summary>
        private readonly List<Unit> allies = new List<Unit>();

        /// <summary>The score of each enemy met, in the order of met.</summary>
        private readonly List<float> scores = new List<float>();

        /// <summary>One rule's value of each enemy met, in the order of met.</summary>
        private readonly List<float> values = new List<float>();

        /// <summary>The allies, most hurt first, for a spell on an ally.</summary>
        private readonly List<Unit> hurt = new List<Unit>();

        /// <summary>The game's seconds at the last check; -1 before any.</summary>
        private float checkedAt = -1f;

        /// <summary>The timer that wakes the brain once its unit stops carrying out a cast; null when none runs.</summary>
        private Timer? castWatch;

        /// <summary>Whether the spot where the fight started is kept, which the leash counts from.</summary>
        internal bool hasSpot;

        /// <summary>The x of the spot where the fight started.</summary>
        internal float spotX;

        /// <summary>The y of the spot where the fight started.</summary>
        internal float spotY;

        /// <summary>A fighter for a unit brain, with the defaults: its unit's acquisition range, a leash of 2,100, the nearest enemy first, no spells, and pulling back below 30% of its life or outnumbered two to one, back at 70%.</summary>
        public Fighter(UnitBrain brain)
        {
            this.brain = brain;
        }

        /// <summary>The route the fighter walks with <c>WalkLane</c> and pulls back along; null, the default, stands and fights where it is and goes back there after a fight.</summary>
        [LocalSafe]
        public Route? Route { get; set; }

        /// <summary>How far the fighter meets enemies; 0, the default, takes its unit's acquisition range, read at each check.</summary>
        [LocalSafe]
        public float MeetRange { get; set; }

        /// <summary>How far from where its fight started an enemy is fought: 2,100, the default, about the game's own chase limit.</summary>
        [LocalSafe]
        public float Leash { get; set; } = 2100f;

        /// <summary>The rules the target is picked by, in the order added; with none, the nearest.</summary>
        [LocalSafe]
        public List<Target> Targets { get; } = new List<Target>();

        /// <summary>The spells the fighter casts while it fights, the first in the list first.</summary>
        [LocalSafe]
        public List<Spell> Spells { get; } = new List<Spell>();

        /// <summary>When the fighter pulls back and comes back; a Below and an Outnumbered of 0 never pull back.</summary>
        [LocalSafe]
        public PullBack PullBack { get; set; } = new PullBack();

        /// <summary>The enemy picked at the last check; null when it met none, or rules ruled each out.</summary>
        [LocalSafe]
        public Unit? Target { get; private set; }

        /// <summary>The enemies in meet range at the last check that it would fight, structures left out.</summary>
        [LocalSafe]
        public int EnemiesNear { get; private set; }

        /// <summary>The allied units in meet range at the last check, its own unit counted and structures left out.</summary>
        [LocalSafe]
        public int AlliesNear { get; private set; }

        /// <summary>Whether the fighter pulls back: true from the <c>ShouldPullBack</c> that found it hurt or outnumbered until the one that found it well enough to come back.</summary>
        [LocalSafe]
        public bool PullingBack { get; private set; }

        /// <summary>Whether the fighter meets an enemy to fight: after a check, whether it has a target. A <c>When</c> watches it.</summary>
        public bool Meets()
        {
            Check();
            return Target != null;
        }

        /// <summary>
        /// Whether the fighter is to pull back, after a check: below its PullBack's share of its life, or outnumbered, enemies
        /// in meet range at least its Outnumbered times the allies there. Once pulling back, it holds until its life is at
        /// or above ComeBackAt and it is not outnumbered. False for a dead unit. A <c>When</c> watches it.
        /// </summary>
        public bool ShouldPullBack()
        {
            Check();
            var settings = PullBack;
            var unit = brain.Unit;
            if (unit == null || !unit.IsAlive)
            {
                PullingBack = false;
                return false;
            }

            var most = unit.MaxLife;
            var share = most > 0f ? unit.Life / most : 1f;
            var outnumbered = settings.Outnumbered > 0f && EnemiesNear >= settings.Outnumbered * AlliesNear;
            PullingBack = outnumbered || share < (PullingBack ? settings.ComeBackAt : settings.Below);
            return PullingBack;
        }

        /// <summary>Notes the damage's source as an attacker, met for 3 s however far, and wakes the brain when it is new: call it from the brain's <c>OnDamaged</c>. A source that is no enemy of the unit's owner is left out.</summary>
        public void Hurt(Damage damage)
        {
            var source = damage.Source;
            var unit = brain.Unit;
            if (source == null || unit == null || source == unit || !unit.Owner.IsEnemy(source.Owner))
            {
                return;
            }

            var now = Game.Time;
            var at = attackers.IndexOf(source);
            if (at < 0)
            {
                attackers.Add(source);
                hitAt.Add(now);
            }
            else
            {
                var known = now - hitAt[at] <= Memory;
                hitAt[at] = now;
                if (known)
                {
                    return;
                }
            }

            brain.Wake();
        }

        /// <summary>
        /// Wakes the brain at the first 0.02 s step at which its unit's order id is no longer the cast's, so the fight attacks
        /// again at once: at a cast's end the unit reads idle and picks a target by itself 0.01 s later, which full control
        /// answers with hold position without waking the brain (game facts, 11-14). A new watch ends the one before.
        /// </summary>
        internal void WakeAfterCast(Unit unit, int castId)
        {
            castWatch?.End();
            castWatch = brain.Every(0.02f, () =>
            {
                if (Runtime.World.CurrentOrderOf(unit.Handle) == castId)
                {
                    return;
                }

                castWatch?.End();
                castWatch = null;
                brain.Wake();
            });
        }

        /// <summary>Whether a fighter with no route has a spot to go back to after its fight.</summary>
        internal bool ReturnsToSpot()
        {
            return Route == null && hasSpot;
        }

        /// <summary>The ally in meet range at the last check, its own unit counted, for whom the condition holds with the least share of its most life, the condition checked from the most hurt; without a condition, the most hurt whose life is below its most. Null for none.</summary>
        internal Unit? MostHurtAlly(Func<Unit, bool>? condition)
        {
            hurt.Clear();
            for (var i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                var share = ShareOf(ally);
                var at = hurt.Count;
                while (at > 0 && ShareOf(hurt[at - 1]) > share)
                {
                    at--;
                }

                hurt.Insert(at, ally);
            }

            for (var i = 0; i < hurt.Count; i++)
            {
                var ally = hurt[i];
                if (ally.IsAlive && (condition != null ? condition(ally) : ally.Life < ally.MaxLife))
                {
                    return ally;
                }
            }

            return null;
        }

        /// <summary>Checks once an instant, by the game's time.</summary>
        private void Check()
        {
            var now = Game.Time;
            if (now == checkedAt)
            {
                return;
            }

            checkedAt = now;
            Refresh(now);
        }

        /// <summary>
        /// Finds the enemies met and the allies near, picks the target, and keeps or forgets the spot where the fight
        /// started: kept from the first check that meets an enemy, forgotten at one that meets none with the unit back
        /// within 64 of a point of its route, or of the spot itself when it has no route.
        /// </summary>
        private void Refresh(float now)
        {
            met.Clear();
            allies.Clear();
            EnemiesNear = 0;
            AlliesNear = 0;
            var unit = brain.Unit;
            if (unit == null || !unit.IsAlive)
            {
                Target = null;
                return;
            }

            for (var i = attackers.Count - 1; i >= 0; i--)
            {
                if (now - hitAt[i] > Memory)
                {
                    attackers.RemoveAt(i);
                    hitAt.RemoveAt(i);
                }
            }

            var owner = unit.Owner;
            var x = unit.X;
            var y = unit.Y;
            var fromX = hasSpot ? spotX : x;
            var fromY = hasSpot ? spotY : y;
            var range = MeetRange > 0f ? MeetRange : Runtime.World.AcquireRangeOf(unit.Handle);
            allies.Add(unit);
            AlliesNear = 1;
            if (range > 0f)
            {
                var found = Runtime.World.UnitsInRange(x, y, range);
                for (var i = 0; i < found.Count; i++)
                {
                    var other = Unit.Of(found[i]);
                    if (other == null || other == unit || !other.IsAlive)
                    {
                        continue;
                    }

                    var structure = Runtime.World.IsStructure(other.Handle);
                    if (owner.IsEnemy(other.Owner))
                    {
                        if (Meetable(owner, other, fromX, fromY))
                        {
                            met.Add(other);
                            if (!structure)
                            {
                                EnemiesNear++;
                            }
                        }
                    }
                    else if (!structure && owner.IsAlly(other.Owner))
                    {
                        allies.Add(other);
                        AlliesNear++;
                    }
                }
            }

            for (var i = 0; i < attackers.Count; i++)
            {
                var attacker = attackers[i];
                if (attacker.IsAlive && !met.Contains(attacker) && owner.IsEnemy(attacker.Owner) && Meetable(owner, attacker, fromX, fromY))
                {
                    met.Add(attacker);
                }
            }

            Choose(unit);
            if (met.Count > 0)
            {
                if (!hasSpot)
                {
                    hasSpot = true;
                    spotX = x;
                    spotY = y;
                }
            }
            else if (hasSpot && IsBack(x, y))
            {
                hasSpot = false;
            }
        }

        /// <summary>Whether an enemy is to be met: its owner's player sees it, and it is within the leash of the spot.</summary>
        private bool Meetable(Player owner, Unit other, float fromX, float fromY)
        {
            var dx = other.X - fromX;
            var dy = other.Y - fromY;
            return dx * dx + dy * dy <= Leash * Leash && Runtime.World.Sees(owner.Handle, other.Handle);
        }

        /// <summary>Whether the unit is back where a fight takes it from: within 64 of a point of its route, or of the spot with no route.</summary>
        private bool IsBack(float x, float y)
        {
            var route = Route;
            if (route == null)
            {
                return Squared(spotX - x, spotY - y) <= Reached;
            }

            var points = route.Points;
            for (var i = 0; i < points.Count; i++)
            {
                if (Squared(points[i].X - x, points[i].Y - y) <= Reached)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Picks the target: the enemy met with the best score, the first among equals, unless the current target is still met and no other scores more than its score times 1.1. An enemy scoring 0 is never picked.</summary>
        private void Choose(Unit unit)
        {
            var current = Target;
            Target = null;
            if (met.Count == 0)
            {
                return;
            }

            Score(unit);
            var best = -1;
            var bestScore = 0f;
            var currentScore = 0f;
            for (var i = 0; i < met.Count; i++)
            {
                var score = scores[i];
                if (met[i] == current)
                {
                    currentScore = score;
                }

                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (best < 0)
            {
                return;
            }

            Target = currentScore > 0f && !(bestScore > currentScore * Keep) ? current : met[best];
        }

        /// <summary>Scores each enemy met: its rules' scores, each raised to its weight, multiplied, then raised to 1 over the weights' sum; 0 once a rule scores it 0. With no rules, by Nearest.</summary>
        private void Score(Unit unit)
        {
            scores.Clear();
            for (var i = 0; i < met.Count; i++)
            {
                scores.Add(1f);
            }

            var weights = 0f;
            if (Targets.Count == 0)
            {
                Apply(Combat.Target.Nearest, unit);
                weights = 1f;
            }

            for (var i = 0; i < Targets.Count; i++)
            {
                Apply(Targets[i], unit);
                weights += Targets[i].weight;
            }

            if (weights == 1f)
            {
                return;
            }

            for (var i = 0; i < scores.Count; i++)
            {
                if (scores[i] > 0f)
                {
                    scores[i] = weights > 0f ? (float)Math.Pow(scores[i], 1.0 / weights) : 1f;
                }
            }
        }

        /// <summary>Multiplies each enemy's score by one rule's score of it raised to the rule's weight, or makes it 0 when the rule scores 0.</summary>
        private void Apply(Target rule, Unit unit)
        {
            values.Clear();
            var lowest = rule.rule == Combat.Target.NearestRule || rule.rule == Combat.Target.WeakestRule;
            var best = lowest ? float.MaxValue : 0f;
            for (var i = 0; i < met.Count; i++)
            {
                var value = ValueOf(rule, met[i], unit);
                values.Add(value);
                if (lowest ? value < best : value > best)
                {
                    best = value;
                }
            }

            for (var i = 0; i < met.Count; i++)
            {
                if (scores[i] <= 0f)
                {
                    continue;
                }

                var value = values[i];
                float score;
                switch (rule.rule)
                {
                    case Combat.Target.NearestRule:
                        score = (float)Math.Sqrt(best / value);
                        break;
                    case Combat.Target.WeakestRule:
                        score = value > 0f ? best / value : 1f;
                        break;
                    case Combat.Target.DangerRule:
                        score = value / best;
                        break;
                    default:
                        score = value;
                        break;
                }

                if (score <= 0f)
                {
                    scores[i] = 0f;
                }
                else
                {
                    scores[i] *= rule.weight == 1f ? score : (float)Math.Pow(score, rule.weight);
                }
            }
        }

        /// <summary>What a rule reads of an enemy: its distance squared, at least 16 squared; its life; its damage a second, at least 1; 1 or 0.5 for Attacker; the map's score clamped to 0..1 for By.</summary>
        private float ValueOf(Target rule, Unit other, Unit unit)
        {
            switch (rule.rule)
            {
                case Combat.Target.NearestRule:
                    return Math.Max(NearestFloor, Squared(other.X - unit.X, other.Y - unit.Y));
                case Combat.Target.WeakestRule:
                    return other.Life;
                case Combat.Target.DangerRule:
                    var cooldown = Runtime.World.AttackCooldownOf(other.Handle);
                    var perSecond = cooldown > 0f ? Runtime.World.AverageDamageOf(other.Handle) / cooldown : 0f;
                    return Math.Max(DangerFloor, perSecond);
                case Combat.Target.AttackerRule:
                    return attackers.Contains(other) ? 1f : 0.5f;
                default:
                    return Math.Clamp(rule.by!(other), 0f, 1f);
            }
        }

        /// <summary>A unit's life as a share of its most; 1 with no most.</summary>
        private static float ShareOf(Unit unit)
        {
            var most = unit.MaxLife;
            return most > 0f ? unit.Life / most : 1f;
        }

        private static float Squared(float dx, float dy)
        {
            return dx * dx + dy * dy;
        }
    }
}
