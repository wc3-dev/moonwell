namespace Moonwell.Spells
{
    /// <summary>A number of a spell template with a value for each level, from level 1: <c>Damage = 75f</c> for every level, <c>Damage = new PerLevel(75f, 150f, 225f)</c> for one each. A level past the last takes the last.</summary>
    public sealed class PerLevel
    {
        /// <summary>The value of each level, in order.</summary>
        private readonly float[] values;

        /// <summary>Holds a value for each level, in order; none is 0 at every level.</summary>
        public PerLevel(params float[] values)
        {
            this.values = values.Length > 0 ? values : new float[] { 0.0f };
        }

        /// <summary>One value for every level.</summary>
        public static implicit operator PerLevel(float value)
        {
            return new PerLevel(value);
        }

        /// <summary>The value at a level: below 1 takes level 1's, past the last the last one's.</summary>
        [LocalSafe]
        public float At(int level)
        {
            var index = level < 1 ? 0 : level > values.Length ? values.Length - 1 : level - 1;
            return values[index];
        }
    }
}
