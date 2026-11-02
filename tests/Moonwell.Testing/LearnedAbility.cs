namespace Moonwell.Testing
{
    /// <summary>An ability that logs when its hero learns it or raises its level.</summary>
    public class LearnedAbility : Ability
    {
        /// <summary>Logs the ability's new level.</summary>
        public override void OnLearned()
        {
            TestLog.Lines.Add("ability learned level " + Level);
        }
    }
}
