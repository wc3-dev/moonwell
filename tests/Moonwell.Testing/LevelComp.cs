namespace Moonwell.Testing
{
    /// <summary>A unit component that logs its hero's level-ups and the skills it learns.</summary>
    public class LevelComp : UnitComponent
    {
        /// <summary>Logs the level reached.</summary>
        public override void OnLevelUp(int reachedLevel)
        {
            TestLog.Lines.Add("comp level " + reachedLevel);
        }

        /// <summary>Logs the skill's level.</summary>
        public override void OnSkillLearned(Ability learnedAbility)
        {
            TestLog.Lines.Add("comp learned level " + learnedAbility.Level);
        }
    }
}
