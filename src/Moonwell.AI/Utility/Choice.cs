using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// Options a brain picks the best of at each think, by their scores: <c>choice.Run(Blackboard)</c> in <c>Think</c>. The
    /// option picked last stays until another scores more than its score times (1 + <see cref="Keep"/>), so the brain does
    /// not flip between two options scoring about the same. A choice belongs to one brain, as it holds the option it picked:
    /// make it in the brain's constructor, where the options' actions are the brain's methods.
    /// </summary>
    public sealed class Choice : IEnumerable<Option>
    {
        /// <summary>The options, in the order added.</summary>
        internal readonly List<Option> options = new List<Option>();

        /// <summary>How much more than the current option's score another needs to win, as a part of it.</summary>
        private float keep = 0.1f;
#if DEBUG

        /// <summary>Whether Pick has the AI tools' work to do: list the choice on the blackboard of its first, then have a forced option win.</summary>
        private bool tooled = true;

        /// <summary>Whether the choice is listed on the blackboard of its first Pick.</summary>
        private bool listed;

        /// <summary>The option the AI tools' -force has win at every Pick, whatever the scores; null for none.</summary>
        internal Option? forced;

        /// <summary>The option the AI tools' log last saw as Current.</summary>
        internal Option? logged;
#endif

        /// <summary>Adds an option, after those added before.</summary>
        public void Add(Option option)
        {
            options.Add(option);
        }

        /// <summary>How much more than the current option's score another needs to win, as a part of it: 0.1, the default, and a current option at 0.73 stays against 0.80 and gives way to 0.81. Below 0 throws an ArgumentException.</summary>
        [LocalSafe]
        public float Keep
        {
            get { return keep; }
            set
            {
                if (!(value >= 0f))
                {
                    throw new ArgumentException("The keep of a choice must be 0 or more, not " + value + ".");
                }

                keep = value;
            }
        }

        /// <summary>The option the last Pick gave; null before any, or when it gave null.</summary>
        [LocalSafe]
        public Option? Current { get; private set; }

        /// <summary>
        /// Scores every option once, in the order added, and gives the one to run, which becomes <see cref="Current"/>. The
        /// best is the highest score, the one added first on a tie. With no current option, the best wins if it scores above
        /// 0. With one, it stays unless the best scores more than its score times (1 + Keep); a current option scoring 0 gives
        /// way to anything above 0. When every option scores 0, or there are none, it gives null.
        /// </summary>
        public Option? Pick(Blackboard board)
        {
            Option? best = null;
            var bestScore = 0f;
            var currentScore = 0f;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var score = option.Score(board);
                if (score > bestScore)
                {
                    best = option;
                    bestScore = score;
                }

                if (option == Current)
                {
                    currentScore = score;
                }
            }

            if (best == null)
            {
                Current = null;
            }
            else if (Current == null || bestScore > currentScore * (1f + keep))
            {
                Current = best;
            }

#if DEBUG
            if (tooled)
            {
                Tool(board);
            }
#endif
            return Current;
        }

        /// <summary>Picks an option, then runs it; nothing when Pick gives null.</summary>
        public void Run(Blackboard board)
        {
            Pick(board)?.Run();
        }

        /// <summary>The options in the order added.</summary>
        public IEnumerator<Option> GetEnumerator()
        {
            return options.GetEnumerator();
        }

        /// <summary>The options in the order added.</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return options.GetEnumerator();
        }
#if DEBUG

        /// <summary>-force: has an option win at every Pick whatever the scores, or no longer with null.</summary>
        internal void Force(Option? option)
        {
            forced = option;
            tooled = true;
        }

        /// <summary>Lists the choice on the blackboard of its first Pick, for the AI tools' view, and has a forced option win.</summary>
        private void Tool(Blackboard board)
        {
            if (!listed)
            {
                listed = true;
                board.AddChoice(this);
            }

            if (forced != null)
            {
                Current = forced;
            }

            tooled = forced != null;
        }
#endif
    }
}
