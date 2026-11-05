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

    /// <summary>A game component that overrides no reaction of the day.</summary>
    public class QuietGameWatch : GameComponent
    {
    }
}
