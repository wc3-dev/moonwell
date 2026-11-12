namespace Moonwell
{
    /// <summary>The kinds of animation the game knows, as SpecialEffect.PlayAnimation takes them: the first word of an animation's name. Which of a kind plays is picked by the effect's sub-animations (SubAnimation). Each is the game's own number (ConvertAnimType).</summary>
    public enum AnimationType
    {
        /// <summary>The model appearing.</summary>
        Birth = 0,

        /// <summary>The model dying.</summary>
        Death = 1,

        /// <summary>A corpse decaying.</summary>
        Decay = 2,

        /// <summary>The model fading away.</summary>
        Dissipate = 3,

        /// <summary>The model standing.</summary>
        Stand = 4,

        /// <summary>The model walking.</summary>
        Walk = 5,

        /// <summary>The model attacking.</summary>
        Attack = 6,

        /// <summary>The model changing form.</summary>
        Morph = 7,

        /// <summary>The model asleep.</summary>
        Sleep = 8,

        /// <summary>The model casting.</summary>
        Spell = 9,

        /// <summary>The model in a unit's portrait.</summary>
        Portrait = 10,
    }
}
