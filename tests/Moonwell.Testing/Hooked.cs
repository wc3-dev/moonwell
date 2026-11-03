using System;

namespace Moonwell.Testing
{
    /// <summary>A refreshed buff that lasts until it is removed, runs an action of a case's in OnApply, and logs its removal.</summary>
    public sealed class Hooked : Buff
    {
        /// <summary>What OnApply runs; nothing unless a case sets it.</summary>
        public Action? WhenApplied;

        /// <summary>Runs WhenApplied.</summary>
        public override void OnApply()
        {
            WhenApplied?.Invoke();
        }

        /// <summary>Logs "hooked removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("hooked removed");
        }
    }

    /// <summary>A strongest buff that runs an action of a case's in OnApply, and logs its removal.</summary>
    public sealed class StrongHooked : Buff
    {
        /// <summary>What OnApply runs; nothing unless a case sets it.</summary>
        public Action? WhenApplied;

        /// <summary>Makes a strongest buff of strength 1 that lasts until it is removed.</summary>
        public StrongHooked()
        {
            Stacking = Stacking.Strongest;
            Strength = 1.0f;
        }

        /// <summary>Runs WhenApplied.</summary>
        public override void OnApply()
        {
            WhenApplied?.Invoke();
        }

        /// <summary>Logs "strong hooked removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("strong hooked removed");
        }
    }
}