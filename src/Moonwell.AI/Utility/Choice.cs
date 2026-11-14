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
        private readonly List<Option> options = new List<Option>();

        /// <summary>How much more than the current option's score another needs to win, as a part of it.</summary>
        private float keep = 0.1f;

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
    }
}
