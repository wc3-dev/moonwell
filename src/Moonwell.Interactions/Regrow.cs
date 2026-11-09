namespace Moonwell
{
    /// <summary>Brings the destructable it is on back to life some seconds after each death, with its birth animation unless told not to: a tree that regrows (decision 230). A gate's Open is no death, and Restore before the time is up stops the wait.</summary>
    public sealed class Regrow : DestructableComponent
    {
        private Timer? waiting;

        /// <summary>Makes the component: the seconds from a death to the destructable's return, and whether it plays its birth animation.</summary>
        public Regrow(float seconds, bool showBirth = true)
        {
            Seconds = seconds;
            ShowBirth = showBirth;
        }

        /// <summary>The seconds from a death to the destructable's return.</summary>
        [LocalSafe]
        public float Seconds { get; }

        /// <summary>Whether the destructable plays its birth animation as it returns.</summary>
        [LocalSafe]
        public bool ShowBirth { get; }

        /// <summary>Starts the wait for the destructable's return.</summary>
        public override void OnDeath()
        {
            waiting?.End();
            waiting = After(Seconds, () => Destructable.Restore(ShowBirth));
        }

        /// <summary>Stops the wait, as something else brought the destructable back.</summary>
        public override void OnRestored()
        {
            waiting?.End();
            waiting = null;
        }
    }
}
