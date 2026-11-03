namespace Moonwell.Testing
{
    /// <summary>A buff of the strongest stacking whose constructor takes its strength and seconds, and which logs each apply with the strength it runs at.</summary>
    public sealed class Strong : Buff
    {
        /// <summary>Makes a buff of a strength for the seconds (0 lasts until it is removed).</summary>
        public Strong(float strength, float seconds = 0.0f)
        {
            Stacking = Stacking.Strongest;
            Strength = strength;
            Duration = seconds;
        }

        /// <summary>Logs "apply" and the strength, as a whole number so .NET and Lua write it the same.</summary>
        public override void OnApply()
        {
            TestLog.Lines.Add("apply " + (int)Strength);
        }

        /// <summary>Logs "strong expire".</summary>
        public override void OnExpire()
        {
            TestLog.Lines.Add("strong expire");
        }

        /// <summary>Logs "strong removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("strong removed");
        }
    }
}
