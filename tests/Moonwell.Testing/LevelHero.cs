namespace Moonwell.Testing
{
    /// <summary>A hero that starts with a LevelComp, and logs its level-ups and the skills it learns.</summary>
    public class LevelHero : Hero
    {
        /// <summary>The level at which its OnLevelUp removes the hero; 0 for none.</summary>
        public static int RemoveAt;

        /// <summary>The level at which its OnLevelUp raises the hero to RaiseTo; 0 for none.</summary>
        public static int RaiseAt;

        /// <summary>The level its OnLevelUp raises the hero to at RaiseAt.</summary>
        public static int RaiseTo;

        /// <summary>Makes a hero that starts with a LevelComp.</summary>
        public LevelHero()
        {
            Components.Add(new LevelComp());
        }

        /// <summary>Logs the level reached and the level the hero reads.</summary>
        public override void OnLevelUp(int reachedLevel)
        {
            TestLog.Lines.Add("hero level " + reachedLevel + " reads " + Level);
            if (reachedLevel == RemoveAt)
            {
                Remove();
            }

            if (reachedLevel == RaiseAt)
            {
                SetLevel(RaiseTo);
            }
        }

        /// <summary>Logs the skill's level and the skill points the hero reads.</summary>
        public override void OnSkillLearned(Ability learnedAbility)
        {
            TestLog.Lines.Add("hero learned level " + learnedAbility.Level + " points " + SkillPoints);
        }
    }
}
