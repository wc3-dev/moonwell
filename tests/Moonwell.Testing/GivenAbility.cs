namespace Moonwell.Testing
{
    /// <summary>An ability that logs when it is learned or lost, and can lower its own level when it is learned.</summary>
    public class GivenAbility : Ability
    {
        /// <summary>The level at which its OnLearned sets the level to 1; 0 for none.</summary>
        public static int LowerAt;

        /// <summary>Logs the ability's new level, and lowers it at LowerAt.</summary>
        public override void OnLearned()
        {
            TestLog.Lines.Add("ability learned level " + Level);
            if (Level == LowerAt)
            {
                Level = 1;
            }
        }

        /// <summary>Logs the level the ability reads once it is lost.</summary>
        public override void OnLost()
        {
            TestLog.Lines.Add("ability lost level " + Level);
        }
    }
}
