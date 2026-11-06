namespace Moonwell.Testing
{
    /// <summary>A game component that logs dawn and dusk, with whether it is day as each runs.</summary>
    public class DayWatch : GameComponent
    {
        /// <summary>Logs the dawn.</summary>
        public override void OnDawn()
        {
            TestLog.Lines.Add("dawn " + (Game.IsDay ? "day" : "night"));
        }

        /// <summary>Logs the dusk.</summary>
        public override void OnDusk()
        {
            TestLog.Lines.Add("dusk " + (Game.IsDay ? "day" : "night"));
        }
    }

    /// <summary>A DayWatch whose dawn sets the time to night, 20 o'clock.</summary>
    public class NightMaker : DayWatch
    {
        /// <summary>Logs the dawn, then sets the time to 20.</summary>
        public override void OnDawn()
        {
            base.OnDawn();
            Game.TimeOfDay = 20.0f;
        }
    }

    /// <summary>A game component that overrides no reaction of the day.</summary>
    public class QuietGameWatch : GameComponent
    {
    }
}
