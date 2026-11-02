namespace Moonwell.Testing
{
    /// <summary>A player component that logs the level-ups and learned skills of heroes, under its label.</summary>
    public class PlayerLevelWatch : PlayerComponent
    {
        /// <summary>The name written at the start of each line.</summary>
        private readonly string label;

        /// <summary>Makes a watch that logs under a label.</summary>
        public PlayerLevelWatch(string label)
        {
            this.label = label;
        }

        /// <summary>Logs the level reached under the label.</summary>
        public override void OnHeroLevelUp(Hero leveledHero, int reachedLevel)
        {
            TestLog.Lines.Add(label + " level " + reachedLevel);
        }

        /// <summary>Logs the skill's level under the label.</summary>
        public override void OnHeroSkillLearned(Hero learningHero, Ability learnedAbility)
        {
            TestLog.Lines.Add(label + " learned level " + learnedAbility.Level);
        }
    }
}
