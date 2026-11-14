using System;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// A rule a fighter scores each enemy it meets by, from 0 to 1, for its <c>Targets</c>: <c>Target.Nearest</c>,
    /// <c>Target.Weakest</c>, <c>Target.MostDangerous</c>, <c>Target.Attacker</c> or a map's own <c>Target.By(...)</c>, and
    /// <c>Weight</c> to count one more. A built-in rule scores an enemy against the others met at the same check, so the
    /// best of them scores 1 and none scores 0; only a map's own rule rules an enemy out, by scoring it 0. A rule never
    /// changes once made, so one is shared freely.
    /// </summary>
    [LocalSafe]
    public sealed class Target
    {
        /// <summary>The rule of the nearest.</summary>
        internal const int NearestRule = 0;

        /// <summary>The rule of the least life left.</summary>
        internal const int WeakestRule = 1;

        /// <summary>The rule of the most damage a second.</summary>
        internal const int DangerRule = 2;

        /// <summary>The rule of who hit the fighter.</summary>
        internal const int AttackerRule = 3;

        /// <summary>A map's own rule.</summary>
        internal const int ByRule = 4;

        /// <summary>Which rule it is.</summary>
        internal readonly int rule;

        /// <summary>The map's function, for a map's own rule; null for any other.</summary>
        internal readonly Func<Unit, float>? by;

        /// <summary>The power the rule's score is raised to in the enemy's score.</summary>
        internal readonly float weight;

        /// <summary>Makes a rule.</summary>
        private Target(int rule, Func<Unit, float>? by, float weight)
        {
            this.rule = rule;
            this.by = by;
            this.weight = weight;
        }

        /// <summary>The nearest first: the distance to the nearest enemy met over the distance to this one, a distance below 16 counted as 16.</summary>
        public static Target Nearest => new Target(NearestRule, null, 1f);

        /// <summary>The weakest first: the least life left among the enemies met over this one's, as amounts, not shares of their most life.</summary>
        public static Target Weakest => new Target(WeakestRule, null, 1f);

        /// <summary>The most dangerous first: this one's damage a second over the most among the enemies met. Its damage a second is the middle of its white damage over its attack cooldown, and below 1 counts as 1.</summary>
        public static Target MostDangerous => new Target(DangerRule, null, 1f);

        /// <summary>Who hits the fighter first: 1 for an enemy that damaged the fighter's unit in the last 3 s, 0.5 for any other.</summary>
        public static Target Attacker => new Target(AttackerRule, null, 1f);

        /// <summary>A map's own rule: the function's score of each enemy met, clamped to 0..1, and 0 rules the enemy out: <c>Target.By(u =&gt; u is Hero ? 1f : 0.3f)</c>. A null function throws an ArgumentNullException.</summary>
        public static Target By(Func<Unit, float> score)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            return new Target(ByRule, score, 1f);
        }

        /// <summary>The same rule with a weight, the power its score is raised to: <c>Target.Weakest.Weight(2f)</c> counts as two rules, and 0 leaves the score out but for ruling an enemy out. The rule itself is not changed. Below 0 throws an ArgumentException.</summary>
        public Target Weight(float weight)
        {
            if (!(weight >= 0f))
            {
                throw new ArgumentException("The weight of a target rule must be 0 or more, not " + weight + ".");
            }

            return new Target(rule, by, weight);
        }
    }
}
