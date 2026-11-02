namespace Moonwell.Build
{
    /// <summary>The experience a hero needs for each level, from the gameplay constants.</summary>
    /// <param name="MaxLevel">The top level (<c>MaxHeroLevel</c>).</param>
    /// <param name="Listed">The totals of levels 2 on, in turn (<c>NeedHeroXP</c>).</param>
    /// <param name="PreviousFactor">What the formula multiplies the previous level's total by (<c>NeedHeroXPFormulaA</c>).</param>
    /// <param name="LevelFactor">What the formula multiplies the level by (<c>NeedHeroXPFormulaB</c>).</param>
    /// <param name="Constant">What the formula adds (<c>NeedHeroXPFormulaC</c>).</param>
    public sealed record HeroExperience(int MaxLevel, IReadOnlyList<float> Listed, float PreviousFactor, float LevelFactor, float Constant)
    {
        /// <summary>The standard constants of <c>units/miscgame.txt</c>, whose table the runtime has without the build's (HeroTable in Moonwell).</summary>
        public static HeroExperience Standard { get; } = new HeroExperience(10, new[] { 200f }, 1f, 100f, 0f);

        /// <summary>
        /// The total experience of each level, from level 1's (0) to the top level's: levels 2 on take the listed totals,
        /// then each level n is the previous level's times A, plus n times B, plus C. The game keeps the fraction and
        /// truncates a total when read (seen 11-02); a total past <see cref="int.MaxValue"/> is that.
        /// </summary>
        public int[] Table()
        {
            var table = new int[Math.Max(MaxLevel, 1)];
            var total = 0f;
            for (var level = 2; level <= table.Length; level++)
            {
                total = level - 2 < Listed.Count ? Listed[level - 2] : (PreviousFactor * total) + (LevelFactor * level) + Constant;
                table[level - 1] = total >= int.MaxValue ? int.MaxValue : (int)total;
            }

            return table;
        }
    }
}
