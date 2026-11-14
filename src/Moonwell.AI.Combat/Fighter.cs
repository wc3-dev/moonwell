using System;
using System.Collections.Generic;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// One fighter's settings, and what it found at its last check, for a unit brain: a <c>LaneWalker</c> has one, and a
    /// map's own brain makes one, <c>new Fighter(this)</c>, for the combat nodes of its tree. A check runs once an instant,
    /// from <c>Meets</c> or <c>ShouldPullBack</c>: it finds the enemies the fighter meets and picks its target. It meets a
    /// living enemy unit, structures too, that its unit's owner sees, within its meet range or one that damaged its unit in
    /// the last 3 s however far, and none farther than its leash from where its fight started. An enemy its unit couldn't
    /// attack is set aside for 3 s, and the next best is picked.
    /// </summary>
    public sealed class Fighter
    {
        /// <summary>The seconds an attacker is remembered after its last hit, and an enemy set aside is skipped: 3.</summary>
        internal const float Memory = 3f;

        /// <summary>How near a place the unit is to be back at it, squared: 64, as the unit-action nodes reach a place.</summary>
        internal const float Reached = 64f * 64f;

        /// <summary>The distance below which Nearest counts a distance as this, squared: 16.</summary>
        private const float NearestFloor = 16f * 16f;

        /// <summary>The damage a second below which MostDangerous counts it as this: 1.</summary>
        private const float DangerFloor = 1f;

        /// <summary>How much more than the current target's score another needs to take over, as a factor: more than a tenth.</summary>
        private const float Keep = 1.1f;

        /// <summary>Pulled back for its life.</summary>
        private const int ForLife = 1;

        /// <summary>Pulled back for being outnumbered.</summary>
        private const int ForNumbers = 2;

        /// <summary>The brain whose unit fights.</summary>
        private readonly UnitBrain brain;

        /// <summary>The units that damaged the fighter's unit, as Hurt noted them; those older than Memory are forgotten at a check.</summary>
        private readonly List<Unit> attackers = new List<Unit>();

        /// <summary>When each of the attackers last hit, in the game's seconds.</summary>
        private readonly List<float> hitAt = new List<float>();

        /// <summary>The enemies set aside, as SetAside noted them; those older than Memory are forgotten at a check.</summary>
        private readonly List<Unit> aside = new List<Unit>();

        /// <summary>When each of the enemies set aside was set aside, in the game's seconds.</summary>
        private readonly List<float> asideAt = new List<float>();

        /// <summary>The units the last search found, the list kept for the next.</summary>
        private readonly List<unit> found = new List<unit>();

        /// <summary>The enemies met at the last check, those set aside left out.</summary>
        private readonly List<Unit> met = new List<Unit>();

        /// <summary>The x of each enemy met, in the order of met.</summary>
        private readonly List<float> metX = new List<float>();

        /// <summary>The y of each enemy met, in the order of met.</summary>
        private readonly List<float> metY = new List<float>();

        /// <summary>The allies in meet range at the last check, the fighter's unit first, structures left out.</summary>
        private readonly List<Unit> allies = new List<Unit>();

        /// <summary>The score of each enemy met, in the order of met.</summary>
        private readonly List<float> scores = new List<float>();

        /// <summary>One rule's value of each enemy met, in the order of met.</summary>
        private readonly List<float> values = new List<float>();

        /// <summary>The allies, most hurt first, for a spell on an ally.</summary>
        private readonly List<Unit> hurt = new List<Unit>();

        /// <summary>The share of its most life of each of hurt, in its order.</summary>
        private readonly List<float> shares = new List<float>();

        /// <summary>The game's seconds at the last check; -1 before any.</summary>
        private float checkedAt = -1f;

        /// <summary>The timer that wakes the brain once its unit stops carrying out a cast; null when none runs.</summary>
        private Timer? castWatch;

        /// <summary>Why the fighter pulls back: ForLife, ForNumbers, or 0 when it doesn't.</summary>
        private int pulledFor;

        /// <summary>Whether the pull-back's node found the unit as near where it pulls back to as it gets, though not within 64.</summary>
        internal bool home;

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
        public List<Target> Targets { get; } = new List<Target>();

        /// <summary>The spells the fighter casts while it fights, the first in the list first.</summary>
        public List<Spell> Spells { get; } = new List<Spell>();

        /// <summary>When the fighter pulls back and comes back; a Below and an Outnumbered of 0 never pull back.</summary>
        [LocalSafe]
        public PullBack PullBack { get; set; } = new PullBack();

        /// <summary>The enemy picked at the last check; null when it met none, or rules ruled each out.</summary>
        [LocalSafe]
        public Unit? Target { get; private set; }

        /// <summary>The enemies in meet range at the last check that it would fight, those set aside too, structures left out.</summary>
        [LocalSafe]
        public int EnemiesNear { get; private set; }

        /// <summary>The allied units in meet range at the last check, its own unit counted and structures left out.</summary>
        [LocalSafe]
        public int AlliesNear { get; private set; }

        /// <summary>Whether the fighter pulls back: true from the <c>ShouldPullBack</c> that found it hurt or outnumbered until the one that found it back where it pulls back to, its reason gone.</summary>
        [LocalSafe]
        public bool PullingBack => pulledFor != 0;

        /// <summary>Whether the fighter meets an enemy to fight: after a check, whether it has a target. A <c>When</c> watches it.</summary>
        public bool Meets()
        {
            Check();
            return Target != null;
        }

        /// <summary>
        /// Whether the fighter is to pull back, after a check: below its PullBack's share of its life, or outnumbered, enemies
        /// in meet range at least its Outnumbered times the allies there. Once pulling back, it holds until the unit is back
        /// where it pulls back to (within 64 of its To, its route's start, or where its fight started), then until its own
        /// reason is gone: pulled back hurt, its life at or above ComeBackAt; outnumbered, no longer outnumbered where it
        /// stands. Then it is judged afresh. False for a dead unit. A <c>When</c> watches it.
        /// </summary>
        public bool ShouldPullBack()
        {
            Check();
            var unit = brain.Unit;
            if (unit == null || !unit.IsAlive)
            {
                pulledFor = 0;
                return false;
            }

            var settings = PullBack;
            var share = ShareOf(unit);
            var outnumbered = settings.Outnumbered > 0f && EnemiesNear >= settings.Outnumbered * AlliesNear;
            var wasBack = pulledFor != 0;
            if (wasBack)
            {
                if (!IsHome(unit.X, unit.Y) || (pulledFor == ForLife ? share < settings.ComeBackAt : outnumbered))
                {
                    return true;
                }
            }

            pulledFor = share < settings.Below ? ForLife : outnumbered ? ForNumbers : 0;
            if (!wasBack)
            {
                home = false;
            }

            return pulledFor != 0;
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
        /// again at once: at a cast's end the unit reads idle (game facts, 11-14), and the pick that full control answers with
        /// a wake comes only with an enemy in its acquisition range. A new watch ends the one before.
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

        /// <summary>Sets an enemy its unit couldn't attack aside for 3 s, skipped by target choice though still counted near, and picks the target again from the others met.</summary>
        internal void SetAside(Unit enemy)
        {
            var now = Game.Time;
            var at = aside.IndexOf(enemy);
            if (at < 0)
            {
                aside.Add(enemy);
                asideAt.Add(now);
            }
            else
            {
                asideAt[at] = now;
            }

            at = met.IndexOf(enemy);
            if (at >= 0)
            {
                met.RemoveAt(at);
                metX.RemoveAt(at);
                metY.RemoveAt(at);
            }

            var unit = brain.Unit;
            Choose(unit.X, unit.Y);
        }

        /// <summary>The ally in meet range at the last check, its own unit counted, for whom the condition holds with the least share of its most life, the condition checked from the most hurt; without a condition, the most hurt whose life is below its most. Null for none.</summary>
        internal Unit? MostHurtAlly(Func<Unit, bool>? condition)
        {
            hurt.Clear();
            shares.Clear();
            for (var i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                var share = ShareOf(ally);
                var at = hurt.Count;
                while (at > 0 && shares[at - 1] > share)
                {
                    at--;
                }

                hurt.Insert(at, ally);
                shares.Insert(at, share);
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
        /// started: kept from the first check with a target, forgotten at one without a target with the unit back on its
        /// route, within 64 of a stretch of it, or within 64 of the spot itself when it has no route.
        /// </summary>
        private void Refresh(float now)
        {
            met.Clear();
            metX.Clear();
            metY.Clear();
            allies.Clear();
            EnemiesNear = 0;
            AlliesNear = 0;
            var unit = brain.Unit;
            if (unit == null || !unit.IsAlive)
            {
                Target = null;
                return;
            }

            Forget(attackers, hitAt, now);
            Forget(aside, asideAt, now);
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
                Runtime.World.UnitsInRange(x, y, range, found);
                for (var i = 0; i < found.Count; i++)
                {
                    var other = Unit.Of(found[i]);
                    if (other == null || other == unit || !other.IsAlive)
                    {
                        continue;
                    }

                    var otherOwner = other.Owner;
                    if (owner.IsEnemy(otherOwner))
                    {
                        var otherX = other.X;
                        var otherY = other.Y;
                        if (Meetable(owner, other, otherX, otherY, fromX, fromY))
                        {
                            Meet(other, otherX, otherY);
                            if (!Runtime.World.IsStructure(other.Handle))
                            {
                                EnemiesNear++;
                            }
                        }
                    }
                    else if (owner.IsAlly(otherOwner) && !Runtime.World.IsStructure(other.Handle))
                    {
                        allies.Add(other);
                        AlliesNear++;
                    }
                }
            }

            for (var i = 0; i < attackers.Count; i++)
            {
                var attacker = attackers[i];
                if (attacker.IsAlive && !met.Contains(attacker) && owner.IsEnemy(attacker.Owner))
                {
                    var attackerX = attacker.X;
                    var attackerY = attacker.Y;
                    if (Meetable(owner, attacker, attackerX, attackerY, fromX, fromY))
                    {
                        Meet(attacker, attackerX, attackerY);
                    }
                }
            }

            Choose(x, y);
            if (Target != null)
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

        /// <summary>Drops the units noted longer ago than Memory, with their times.</summary>
        private static void Forget(List<Unit> units, List<float> at, float now)
        {
            for (var i = units.Count - 1; i >= 0; i--)
            {
                if (now - at[i] > Memory)
                {
                    units.RemoveAt(i);
                    at.RemoveAt(i);
                }
            }
        }

        /// <summary>Notes an enemy met at its place, unless it is set aside.</summary>
        private void Meet(Unit other, float otherX, float otherY)
        {
            if (aside.Contains(other))
            {
                return;
            }

            met.Add(other);
            metX.Add(otherX);
            metY.Add(otherY);
        }

        /// <summary>Whether an enemy is to be met: its owner's player sees it, and it is within the leash of the spot.</summary>
        private bool Meetable(Player owner, Unit other, float otherX, float otherY, float fromX, float fromY)
        {
            return Squared(otherX - fromX, otherY - fromY) <= Leash * Leash && Runtime.World.Sees(owner.Handle, other.Handle);
        }

        /// <summary>Whether the unit is back where a fight takes it from: within 64 of a stretch of its route between two points, or of the spot with no route.</summary>
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
                if (SquaredToStretch(x, y, points[i], points[i + 1 < points.Count ? i + 1 : i]) <= Reached)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the unit is where a pull-back takes it: as near as it gets, as the pull-back's node found, or within 64 of its PullBack's To, else of its route's start, else of where its fight started; with none of them, where it stands.</summary>
        private bool IsHome(float x, float y)
        {
            if (home)
            {
                return true;
            }

            var to = PullBack.To;
            if (to != null)
            {
                return Squared(to.Value.X - x, to.Value.Y - y) <= Reached;
            }

            var route = Route;
            if (route != null && route.Points.Count > 0)
            {
                return Squared(route.Points[0].X - x, route.Points[0].Y - y) <= Reached;
            }

            return !hasSpot || Squared(spotX - x, spotY - y) <= Reached;
        }

        /// <summary>Picks the target: the enemy met with the best score, the first in the game's list of units in range among equals, unless the current target is still met and no other scores more than its score times 1.1. An enemy scoring 0 is never picked.</summary>
        private void Choose(float x, float y)
        {
            var current = Target;
            Target = null;
            if (met.Count == 0)
            {
                return;
            }

            Score(x, y);
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
        private void Score(float x, float y)
        {
            scores.Clear();
            for (var i = 0; i < met.Count; i++)
            {
                scores.Add(1f);
            }

            var weights = 0f;
            if (Targets.Count == 0)
            {
                Apply(Combat.Target.Nearest, x, y);
                weights = 1f;
            }

            for (var i = 0; i < Targets.Count; i++)
            {
                Apply(Targets[i], x, y);
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
        private void Apply(Target rule, float x, float y)
        {
            values.Clear();
            var lowest = rule.rule == Combat.Target.NearestRule || rule.rule == Combat.Target.WeakestRule;
            var best = lowest ? float.MaxValue : 0f;
            for (var i = 0; i < met.Count; i++)
            {
                var value = ValueOf(rule, i, x, y);
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

        /// <summary>What a rule reads of the enemy met at an index: its distance squared from the unit at x and y, at least 16 squared; its life; its damage a second, at least 1; 1 or 0.5 for Attacker; the map's score clamped to 0..1 for By.</summary>
        private float ValueOf(Target rule, int index, float x, float y)
        {
            var other = met[index];
            switch (rule.rule)
            {
                case Combat.Target.NearestRule:
                    return Math.Max(NearestFloor, Squared(metX[index] - x, metY[index] - y));
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

        /// <summary>The distance squared from a place to the nearest place on the straight stretch between two points.</summary>
        private static float SquaredToStretch(float x, float y, Vector2 from, Vector2 to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = dx * dx + dy * dy;
            var along = length > 0f ? Math.Clamp(((x - from.X) * dx + (y - from.Y) * dy) / length, 0f, 1f) : 0f;
            return Squared(from.X + along * dx - x, from.Y + along * dy - y);
        }

        private static float Squared(float dx, float dy)
        {
            return dx * dx + dy * dy;
        }
    }
}
