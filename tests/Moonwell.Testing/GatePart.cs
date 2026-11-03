namespace Moonwell.Testing
{
    /// <summary>A destructable component that logs when its destructable dies.</summary>
    public class GatePart : DestructableComponent
    {
        /// <summary>Logs that the part saw the death.</summary>
        public override void OnDeath()
        {
            TestLog.Lines.Add("part died");
        }

        /// <summary>Logs that the part saw the restore.</summary>
        public override void OnRestored()
        {
            TestLog.Lines.Add("part restored");
        }
    }
}
