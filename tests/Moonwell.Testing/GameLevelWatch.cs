namespace Moonwell.Testing
{
    /// <summary>A game component that logs every hero's level-ups and learned skills.</summary>
    public class GameLevelWatch : GameComponent
    {
        /// <summary>Logs the level reached.</summary>
        public override void OnHeroLevelUp(Hero leveledHero, int reachedLevel)
        {
            TestLog.Lines.Add("game level " + reachedLevel);
        }

        /// <summary>Logs the skill's level.</summary>
        public override void OnHeroSkillLearned(Hero learningHero, Ability learnedAbility)
        {
            TestLog.Lines.Add("game learned level " + learnedAbility.Level);
        }
    }
}
