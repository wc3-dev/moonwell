namespace Moonwell.AI.Combat
{
    /// <summary>
    /// When a fighter pulls back and when it comes back, for its <c>PullBack</c>: below a share of its life, or outnumbered
    /// in its meet range, it pulls back all the way, and there it comes back once its own reason is gone: pulled back hurt,
    /// at or above a higher share of its life; outnumbered, no longer outnumbered where it stands.
    /// Set in braces on a lane walker: <c>PullBack = { Below = 0.25f, To = camp }</c>.
    /// </summary>
    public sealed class PullBack
    {
        /// <summary>The share of its most life below which the fighter pulls back: 0.3, the default; 0 never pulls back for its life.</summary>
        [LocalSafe]
        public float Below { get; set; } = 0.3f;

        /// <summary>How many enemies to each ally outnumber the fighter: 2, the default, pulls back with at least two enemies in its meet range to each ally there, its own unit counted and structures on neither side; 0 never pulls back for numbers.</summary>
        [LocalSafe]
        public float Outnumbered { get; set; } = 2f;

        /// <summary>The share of its most life at or above which a fighter that pulled back for its life comes back, once back where it pulls back to: 0.7, the default. One that pulled back outnumbered comes back at any life.</summary>
        [LocalSafe]
        public float ComeBackAt { get; set; } = 0.7f;

        /// <summary>Where the fighter pulls back to, then stands; null, the default, walks back along its route to its start, or with no route to where its fight started.</summary>
        [LocalSafe]
        public Vector2? To { get; set; }
    }
}
