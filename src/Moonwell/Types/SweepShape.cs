namespace Moonwell
{
    /// <summary>The shape of the band a Sweep moves through text.</summary>
    public enum SweepShape
    {
        /// <summary>A band about 4 letters wide.</summary>
        Narrow,

        /// <summary>A soft band about 12 letters wide.</summary>
        Wide,

        /// <summary>One letter in the sweep's colour, its two neighbours half.</summary>
        Sharp,

        /// <summary>A sharp front with a trail of 20 letters fading behind it.</summary>
        Comet,

        /// <summary>A narrow band started every second, so a long text has several at once.</summary>
        Repeating,
    }
}
