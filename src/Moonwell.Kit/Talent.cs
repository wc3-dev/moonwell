using System;

namespace Moonwell
{
    /// <summary>
    /// A talent of a TalentWindow (decision 224): a class the map writes, which the window adds to its hero as a component at
    /// the first rank and removes at a Reset, so it can hold modifiers and override reactions while learned. Its constructor
    /// sets what the window shows and needs: Name, Icon, Text, MaxRank, Tree, Row, Column and Needs. OnRankChanged runs at each
    /// rank, with RankScope new: what it holds lasts until the next rank or the reset.
    /// </summary>
    public abstract class Talent : UnitComponent
    {
        internal RankHold? rankScope;

        /// <summary>The name, the tooltip's title.</summary>
        [LocalSafe]
        public string Name { get; protected set; } = "";

        /// <summary>The icon's texture.</summary>
        [LocalSafe]
        public string Icon { get; protected set; } = "";

        /// <summary>What it does, the tooltip's text.</summary>
        [LocalSafe]
        public string Text { get; protected set; } = "";

        /// <summary>How many ranks it has; 1 unless set.</summary>
        [LocalSafe]
        public int MaxRank { get; protected set; } = 1;

        /// <summary>Its row in the tree, from 0 at the top: row r opens once r × the window's PointsPerRow points are spent in its tree.</summary>
        [LocalSafe]
        public int Row { get; protected set; }

        /// <summary>Its column in the tree, from 0 at the left.</summary>
        [LocalSafe]
        public int Column { get; protected set; }

        /// <summary>The tree it is in, a tab of the window named so; none unless set, for a window of one tree. Every talent of a window names one, or none does.</summary>
        [LocalSafe]
        public string Tree { get; protected set; } = "";

        /// <summary>The rank learned, 0 to MaxRank; on the component the window added, the hero's.</summary>
        [LocalSafe]
        public int Rank { get; internal set; }

        /// <summary>The hero that learned it.</summary>
        public Hero Hero => (Hero)Unit;

        /// <summary>A scope for this rank: it ends at the next rank or the reset, so a modifier held by it is the rank's: Hero.Modify(Stat.Damage, percent: 5 * Rank, heldBy: RankScope).</summary>
        public Scope RankScope => rankScope ?? throw new InvalidOperationException("RankScope is read in OnRankChanged, once the talent is learned");

        /// <summary>The talent of another class that must be at a rank first, set by Needs; null for none.</summary>
        internal Type? needs;

        internal int needsRank;

        /// <summary>Needs a talent of another class in the same window, in any of its trees, at a rank before this one can be learned.</summary>
        protected void Needs<T>(int rank = 1)
            where T : Talent
        {
            needs = typeof(T);
            needsRank = rank;
        }

        /// <summary>Runs when a rank is learned, with Rank the new one and RankScope new, and when the window brings ranks back with Restore, once, at the rank restored.</summary>
        protected virtual void OnRankChanged()
        {
        }

        /// <summary>Ends the last rank's scope, makes the next and runs OnRankChanged.</summary>
        internal void ChangeRank(int rank)
        {
            Rank = rank;
            rankScope?.End();
            rankScope = new RankHold();
            Adopt(rankScope);
            try
            {
                OnRankChanged();
            }
            catch (Exception error)
            {
                Errors.Report(Names.NameOf(this) + ".OnRankChanged", error);
            }
        }
    }

    /// <summary>A talent's scope for one rank.</summary>
    internal sealed class RankHold : Scope
    {
    }
}
