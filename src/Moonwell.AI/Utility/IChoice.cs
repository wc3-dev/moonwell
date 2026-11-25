namespace Moonwell.AI
{
    /// <summary>
    /// What the AI tools read and change of a choice, a <see cref="Choice"/> or a <see cref="Choice{T}"/>, so the brain view,
    /// -force and the log treat both alike; it has no members in a release build.
    /// </summary>
    internal interface IChoice
    {
#if DEBUG
        /// <summary>How many options the choice has.</summary>
        int OptionCount { get; }

        /// <summary>The place of the current option; -1 for none.</summary>
        int CurrentIndex { get; }

        /// <summary>The name of the option at a place.</summary>
        string OptionName(int index);

        /// <summary>The last score of the option at a place: for a choice over a list, its best over the list at the last pick.</summary>
        float OptionScore(int index);

        /// <summary>Whether -force has the option at a place win.</summary>
        bool IsForced(int index);

        /// <summary>-force: has the option at a place win at every pick whatever the scores, or no longer with -1.</summary>
        void Force(int index);

        /// <summary>What the view shows after the current option's score: "  life 0.20, enemies near 4.00", or for a choice over a list the thing first, "  Farm 2  urgency 0.80".</summary>
        string CurrentText();

        /// <summary>Notes what the choice picks now, so the log's next line is for a change.</summary>
        void LogNote();

        /// <summary>The log's text when what the choice picks changed since it was noted, "flee 0.61 (was fight 0.40)", noting it anew; null when it didn't.</summary>
        string? LogChange();
#endif
    }
}
