namespace Moonwell
{
    /// <summary>Tags the game adds to a unit's animation names, as Unit.AddAnimationTag takes them; any other tag the game takes works as text, such as "upgrade first".</summary>
    public static class AnimationTag
    {
        /// <summary>The model's other form, as a Druid of the Talon's: "alternate".</summary>
        public const string Alternate = "alternate";

        /// <summary>Raised shield, as a footman's Defend: "defend".</summary>
        public const string Defend = "defend";

        /// <summary>Carrying gold: "gold".</summary>
        public const string Gold = "gold";

        /// <summary>Carrying lumber: "lumber".</summary>
        public const string Lumber = "lumber";

        /// <summary>Working, as a building at work: "work".</summary>
        public const string Work = "work";
    }
}
