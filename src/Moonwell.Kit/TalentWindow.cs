using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A ready talent tree of the UI kit (decision 224), one hero's: talents as icons on a grid by row and column, each showing
    /// its rank, with a rich tooltip of its name, text, rank and needs. A click by the hero's owner learns one rank when the
    /// hero has a point, the talent is below its most rank, enough points are spent for its row and the talent it needs is
    /// at its rank. The map gives the points (Points += 1, as in OnLevelUp); Ranks and Restore keep the ranks in a save, and
    /// Reset gives every point back.
    /// </summary>
    public class TalentWindow : Window
    {
        /// <summary>A talent's side and the gap between talents.</summary>
        internal const float TalentSize = 0.04f, TalentGap = 0.012f;

        internal readonly List<Func<Talent>> makers = new List<Func<Talent>>();

        /// <summary>What each talent is, made once to be read; never added to the hero.</summary>
        internal readonly List<Talent> kinds = new List<Talent>();

        /// <summary>The component each talent is on the hero; null while not learned.</summary>
        internal readonly List<Talent?> learned = new List<Talent?>();

        internal readonly List<IconButton> buttons = new List<IconButton>();

        internal readonly List<Label> rankLabels = new List<Label>();

        internal int points;

        internal int pointsPerRow = 5;

        internal List<Action<Player, Talent>>? learnedHandlers;

        /// <summary>Makes an empty tree of a hero titled Talents, to be given talents with Add and added to the screen for its owner: Screen.Add(tree, hero, shownTo: hero.Owner).</summary>
        public TalentWindow(Hero hero)
        {
            Hero = hero;
            Title = "Talents";
        }

        /// <summary>The hero the tree is for.</summary>
        [LocalSafe]
        public Hero Hero { get; }

        /// <summary>The points the hero has to spend; 0 unless set. The map gives them.</summary>
        [LocalSafe]
        public int Points
        {
            get { return points; }
            set
            {
                points = value;
                Refresh();
            }
        }

        /// <summary>How many points spent open each row: row r opens at r × this; 5 unless set.</summary>
        [LocalSafe]
        public int PointsPerRow
        {
            get { return pointsPerRow; }
            set
            {
                pointsPerRow = value;
                Refresh();
            }
        }

        /// <summary>The points spent: every rank learned.</summary>
        [LocalSafe]
        public int Spent
        {
            get
            {
                var spent = 0;
                for (var i = 0; i < learned.Count; i++)
                {
                    spent += learned[i]?.Rank ?? 0;
                }

                return spent;
            }
        }

        /// <summary>Runs when the hero's owner learns a rank, after the talent's OnRankChanged, with the player and the talent on the hero. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, Talent>? Learned
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (learnedHandlers ??= new List<Action<Player, Talent>>()).Add(value);
                }
            }

            remove { Unsubscribe(learnedHandlers, value); }
        }

        /// <summary>Puts a talent in the tree, made by the maker each time the hero learns it from rank 0 (a Reset removes the last one), as a map's player components are: tree.Add(() => new Cleave()).</summary>
        public void Add(Func<Talent> make)
        {
            if (HasEnded)
            {
                return;
            }

            var kind = make();
            var at = kinds.Count;
            makers.Add(make);
            kinds.Add(kind);
            learned.Add(null);
            var x = Inset + kind.Column * (TalentSize + TalentGap);
            var y = -Scoreboard.Top - kind.Row * (TalentSize + TalentGap);
            var button = Add(new IconButton { Icon = kind.Icon, Width = TalentSize, Height = TalentSize, Anchor = FramePoint.TopLeft, Offset = new Vector2(x, y) });
            button.Tooltip = new RichTooltip { Icon = kind.Icon, Title = kind.Name };
            button.Clicked += who => Clicked(who, at);
            buttons.Add(button);
            rankLabels.Add(Add(new Label { Anchor = FramePoint.TopLeft, Offset = new Vector2(x + TalentSize - 0.012f, y - TalentSize + 0.008f) }));
            Refresh();
        }

        /// <summary>The rank of each talent, in the order added, for a [Saved] list of a save class.</summary>
        public List<int> Ranks()
        {
            var ranks = new List<int>();
            for (var i = 0; i < learned.Count; i++)
            {
                ranks.Add(learned[i]?.Rank ?? 0);
            }

            return ranks;
        }

        /// <summary>Brings back ranks Ranks gave, after taking off what the hero learned: each talent runs OnRankChanged once, at its rank. Points stay as they are; a save keeps them itself. A rank past a talent's most is held to it, and ranks past the talents are let be.</summary>
        public void Restore(List<int> ranks)
        {
            TakeOff();
            for (var i = 0; i < ranks.Count && i < kinds.Count; i++)
            {
                var rank = Math.Min(ranks[i], kinds[i].MaxRank);
                if (rank > 0)
                {
                    PutOn(i, rank);
                }
            }

            Refresh();
        }

        /// <summary>Takes every rank back, removing the talents from the hero, and gives their points back.</summary>
        public void Reset()
        {
            points += Spent;
            TakeOff();
            Refresh();
        }

        /// <summary>Whether the hero can learn the next rank of a talent now.</summary>
        internal bool CanLearn(int at)
        {
            var kind = kinds[at];
            var rank = learned[at]?.Rank ?? 0;
            return points > 0 && rank < kind.MaxRank && Spent >= kind.Row * pointsPerRow && Met(kind);
        }

        private bool Met(Talent kind)
        {
            if (kind.needs == null)
            {
                return true;
            }

            for (var i = 0; i < kinds.Count; i++)
            {
                if (kinds[i].GetType() == kind.needs)
                {
                    return (learned[i]?.Rank ?? 0) >= kind.needsRank;
                }
            }

            return false;
        }

        private void Clicked(Player who, int at)
        {
            if (who != Hero.Owner || Hero.IsGone || !CanLearn(at))
            {
                return;
            }

            points--;
            var talent = PutOn(at, (learned[at]?.Rank ?? 0) + 1);
            Refresh();
            if (talent != null)
            {
                Raise(learnedHandlers, who, talent, "Learned");
            }
        }

        // Adds the talent's component at its first rank, then sets the rank.
        private Talent? PutOn(int at, int rank)
        {
            var talent = learned[at];
            if (talent == null)
            {
                talent = Hero.Add(makers[at]());
                if (talent.HasEnded)
                {
                    return null;
                }

                learned[at] = talent;
            }

            talent.ChangeRank(rank);
            return talent;
        }

        private void TakeOff()
        {
            for (var i = 0; i < learned.Count; i++)
            {
                var talent = learned[i];
                learned[i] = null;
                if (talent != null && !talent.HasEnded)
                {
                    Hero.Remove(talent);
                }
            }
        }

        // Writes each talent's rank and tooltip, and sizes the window to the grid.
        private void Refresh()
        {
            if (HasEnded)
            {
                return;
            }

            var columns = 1;
            var rows = 1;
            for (var i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                var rank = learned[i]?.Rank ?? 0;
                columns = Math.Max(columns, kind.Column + 1);
                rows = Math.Max(rows, kind.Row + 1);
                rankLabels[i].Text = rank + "/" + kind.MaxRank;
                rankLabels[i].Color = CanLearn(i) ? theme.Title : theme.Text;
                var tip = (RichTooltip)buttons[i].Tooltip!;
                tip.Text = TipText(kind, rank);
            }

            Width = 2.0f * Inset + columns * TalentSize + (columns - 1) * TalentGap;
            Height = Scoreboard.Top + rows * TalentSize + (rows - 1) * TalentGap + Inset;
        }

        private string TipText(Talent kind, int rank)
        {
            var text = kind.Text + "\n\nRank " + rank + "/" + kind.MaxRank;
            if (kind.Row > 0 && Spent < kind.Row * pointsPerRow)
            {
                text += "\nNeeds " + kind.Row * pointsPerRow + " points in the tree";
            }

            if (kind.needs != null && !Met(kind))
            {
                for (var i = 0; i < kinds.Count; i++)
                {
                    if (kinds[i].GetType() == kind.needs)
                    {
                        text += "\nNeeds " + kinds[i].Name + " " + kind.needsRank;
                    }
                }
            }

            return text;
        }
    }
}
