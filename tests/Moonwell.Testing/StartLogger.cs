using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A game component that keeps RealTime.Now's seconds each time the game starts.</summary>
    public sealed class StartLogger : GameComponent
    {
        private readonly List<int> starts;

        /// <summary>Keeps each start in the list.</summary>
        public StartLogger(List<int> starts)
        {
            this.starts = starts;
        }

        /// <summary>Keeps the shared date the start sees.</summary>
        public override void OnGameStart()
        {
            starts.Add(RealTime.Now.Seconds);
        }
    }
}
