namespace Moonwell
{
    /// <summary>The words the game adds after an animation's kind to pick which of that kind plays ("Morph Alternate", "Attack Slam"), as SpecialEffect.AddSubAnimation takes them; what AnimationTag is to a unit. Each is the game's own number (ConvertSubAnimType).</summary>
    public enum SubAnimation
    {
        /// <summary>"rooted".</summary>
        Rooted = 11,

        /// <summary>"alternate", the model's other form (the game's SUBANIM_TYPE_ALTERNATE_EX).</summary>
        Alternate = 12,

        /// <summary>"looping".</summary>
        Looping = 13,

        /// <summary>"slam".</summary>
        Slam = 14,

        /// <summary>"throw".</summary>
        Throw = 15,

        /// <summary>"spiked".</summary>
        Spiked = 16,

        /// <summary>"fast".</summary>
        Fast = 17,

        /// <summary>"spin".</summary>
        Spin = 18,

        /// <summary>"ready".</summary>
        Ready = 19,

        /// <summary>"channel".</summary>
        Channel = 20,

        /// <summary>"defend".</summary>
        Defend = 21,

        /// <summary>"victory".</summary>
        Victory = 22,

        /// <summary>"turn".</summary>
        Turn = 23,

        /// <summary>"left".</summary>
        Left = 24,

        /// <summary>"right".</summary>
        Right = 25,

        /// <summary>"fire".</summary>
        Fire = 26,

        /// <summary>"flesh".</summary>
        Flesh = 27,

        /// <summary>"hit".</summary>
        Hit = 28,

        /// <summary>"wounded".</summary>
        Wounded = 29,

        /// <summary>"light".</summary>
        Light = 30,

        /// <summary>"moderate".</summary>
        Moderate = 31,

        /// <summary>"severe".</summary>
        Severe = 32,

        /// <summary>"critical".</summary>
        Critical = 33,

        /// <summary>"complete".</summary>
        Complete = 34,

        /// <summary>"gold".</summary>
        Gold = 35,

        /// <summary>"lumber".</summary>
        Lumber = 36,

        /// <summary>"work".</summary>
        Work = 37,

        /// <summary>"talk".</summary>
        Talk = 38,

        /// <summary>"first".</summary>
        First = 39,

        /// <summary>"second".</summary>
        Second = 40,

        /// <summary>"third".</summary>
        Third = 41,

        /// <summary>"fourth".</summary>
        Fourth = 42,

        /// <summary>"fifth".</summary>
        Fifth = 43,

        /// <summary>"one".</summary>
        One = 44,

        /// <summary>"two".</summary>
        Two = 45,

        /// <summary>"three".</summary>
        Three = 46,

        /// <summary>"four".</summary>
        Four = 47,

        /// <summary>"five".</summary>
        Five = 48,

        /// <summary>"small".</summary>
        Small = 49,

        /// <summary>"medium".</summary>
        Medium = 50,

        /// <summary>"large".</summary>
        Large = 51,

        /// <summary>"upgrade".</summary>
        Upgrade = 52,

        /// <summary>"drain".</summary>
        Drain = 53,

        /// <summary>"fill".</summary>
        Fill = 54,

        /// <summary>"chainlightning".</summary>
        ChainLightning = 55,

        /// <summary>"eattree".</summary>
        EatTree = 56,

        /// <summary>"puke".</summary>
        Puke = 57,

        /// <summary>"flail".</summary>
        Flail = 58,

        /// <summary>"off".</summary>
        Off = 59,

        /// <summary>"swim".</summary>
        Swim = 60,

        /// <summary>"entangle".</summary>
        Entangle = 61,

        /// <summary>"berserk".</summary>
        Berserk = 62,
    }
}
