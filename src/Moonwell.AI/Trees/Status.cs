namespace Moonwell.AI
{
    /// <summary>What a node of a behaviour tree gives at a pass: it goes on at the next pass, or it has ended, well or not.</summary>
    public enum Status
    {
        /// <summary>The node goes on: it runs again at the next pass.</summary>
        Running,

        /// <summary>The node has ended and done what it was for.</summary>
        Succeeded,

        /// <summary>The node has ended without doing it.</summary>
        Failed,
    }
}
