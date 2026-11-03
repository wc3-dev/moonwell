using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A game component that counts the game's starts, reading nothing.</summary>
    public sealed class StartTally : GameComponent
    {
        private readonly List<int> starts;

        /// <summary>Counts each start in the list.</summary>
        public StartTally(List<int> starts)
        {
            this.starts = starts;
        }

        /// <summary>Counts the start.</summary>
        public override void OnGameStart()
        {
            starts.Add(starts.Count + 1);
        }
    }
}
