using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>An aura of the game a fake world runs.</summary>
    public sealed class FakeAura
    {
        /// <summary>The unit that holds it.</summary>
        public unit Holder = null!;

        /// <summary>The game buff it gives.</summary>
        public int Buff;

        /// <summary>How far it reaches, centre to centre.</summary>
        public float Reach;

        /// <summary>The units it gave its buff to, in reach or still within the 3 s after they left.</summary>
        public List<unit> Given = new List<unit>();

        /// <summary>When each unit with the buff left its reach.</summary>
        public Dictionary<unit, float> LeftAt = new Dictionary<unit, float>();
    }
}
