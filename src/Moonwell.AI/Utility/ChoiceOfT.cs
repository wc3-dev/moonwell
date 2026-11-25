using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// Options a brain picks the best of at each think over a list of things it gives then, each option scored for each
    /// thing by inputs that read it: <c>jobs.Run(Blackboard, planner.Jobs)</c> in <c>Think</c>, and the winning option runs
    /// with the winning thing. The option and thing picked last stay until another pair scores more than their score times
    /// (1 + <see cref="Keep"/>), so the brain does not flip between two jobs scoring about the same. A choice belongs to one
    /// brain, as it holds what it picked: make it in the brain's constructor. The things are classes (a unit, a tree, a job
    /// of the map's own), told apart by reference.
    /// </summary>
    /// <typeparam name="T">The things the options are scored for.</typeparam>
    public sealed class Choice<T> : IEnumerable<Option<T>>, IChoice
        where T : class
    {
        /// <summary>The options, in the order added.</summary>
        internal readonly List<Option<T>> options = new List<Option<T>>();

        /// <summary>How much more than the current pair's score another needs to win, as a part of it.</summary>
        private float keep = 0.1f;
#if DEBUG

        /// <summary>Whether the choice is listed on the blackboard of its first Pick.</summary>
        private bool listed;

        /// <summary>The option the AI tools' -force has win at every Pick, whatever the scores; null for none.</summary>
        private Option<T>? forced;

        /// <summary>The option the AI tools' log last saw as Current.</summary>
        private Option<T>? loggedOption;

        /// <summary>The thing the AI tools' log last saw as CurrentItem.</summary>
        private T? loggedItem;

        /// <summary>The score the pair the log last saw had at the last Pick; 0 when it wasn't scored.</summary>
        private float loggedScore;

        /// <summary>The current pair's score at the last Pick, for the log.</summary>
        private float currentScore;

        /// <summary>The values of the current option's inputs for the current thing at the last Pick, for the view.</summary>
        private readonly List<float> currentValues = new List<float>();

        /// <summary>The values of the inputs of the pair that was current before the Pick, as they were read in it.</summary>
        private readonly List<float> keptValues = new List<float>();

        /// <summary>The values of the inputs of the best pair so far in the Pick.</summary>
        private readonly List<float> bestValues = new List<float>();

        /// <summary>The values of the forced option's inputs for the thing it scored best for, or the first, in the Pick.</summary>
        private readonly List<float> forcedValues = new List<float>();
#endif

        /// <summary>Adds an option, after those added before.</summary>
        public void Add(Option<T> option)
        {
            options.Add(option);
        }

        /// <summary>How much more than the current pair's score another needs to win, as a part of it: 0.1, the default, and a current pair at 0.73 stays against 0.80 and gives way to 0.81. Below 0 throws an ArgumentException.</summary>
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
        public Option<T>? Current { get; private set; }

        /// <summary>The thing the last Pick gave with its option; null when it gave none.</summary>
        [LocalSafe]
        public T? CurrentItem { get; private set; }

        /// <summary>
        /// Scores every option for every thing in the list, the options in the order added and for each the things in the
        /// list's order, and gives the option to run, which becomes <see cref="Current"/>, with its thing as
        /// <see cref="CurrentItem"/>. The best pair is the highest score, the option added first and then the thing first in
        /// the list on a tie. With no current pair, the best wins if it scores above 0. With one, it stays unless the best
        /// scores more than its score times (1 + Keep); a current thing no longer in the list scores 0, and a current pair
        /// scoring 0 gives way to anything above 0. When every pair scores 0, or the list or the options are empty, it gives
        /// null.
        /// </summary>
        public Option<T>? Pick(Blackboard board, IReadOnlyList<T> items)
        {
            Option<T>? best = null;
            T? bestItem = null;
            var bestScore = 0f;
            var keptScore = 0f;
#if DEBUG
            loggedScore = 0f;
#endif
            for (var o = 0; o < options.Count; o++)
            {
                var option = options[o];
                option.lastScore = 0f;
                option.lastItem = null;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var score = option.Score(board, item);
                    var better = score > option.lastScore;
                    if (better)
                    {
                        option.lastScore = score;
                        option.lastItem = item;
                    }
#if DEBUG

                    if (option == forced && (better || i == 0))
                    {
                        option.ValuesInto(forcedValues);
                    }
#endif

                    if (score > bestScore)
                    {
                        best = option;
                        bestItem = item;
                        bestScore = score;
#if DEBUG
                        option.ValuesInto(bestValues);
#endif
                    }

                    if (option == Current && item == CurrentItem)
                    {
                        keptScore = score;
#if DEBUG
                        option.ValuesInto(keptValues);
#endif
                    }
#if DEBUG

                    if (option == loggedOption && item == loggedItem)
                    {
                        loggedScore = score;
                    }
#endif
                }
            }

            var kept = best != null && Current != null && !(bestScore > keptScore * (1f + keep));
            if (best == null)
            {
                Current = null;
                CurrentItem = null;
            }
            else if (!kept)
            {
                Current = best;
                CurrentItem = bestItem;
            }

#if DEBUG
            currentScore = best == null ? 0f : kept ? keptScore : bestScore;
            Tool(board, items, kept);
#endif
            return Current;
        }

        /// <summary>Picks an option and a thing, then runs the option with the thing; nothing when Pick gives null.</summary>
        public void Run(Blackboard board, IReadOnlyList<T> items)
        {
            var option = Pick(board, items);
            if (option != null)
            {
                option.Run(CurrentItem!);
            }
        }

        /// <summary>The options in the order added.</summary>
        public IEnumerator<Option<T>> GetEnumerator()
        {
            return options.GetEnumerator();
        }

        /// <summary>The options in the order added.</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return options.GetEnumerator();
        }
#if DEBUG

        /// <summary>How many options the choice has.</summary>
        int IChoice.OptionCount => options.Count;

        /// <summary>The place of the current option; -1 for none.</summary>
        int IChoice.CurrentIndex => Current != null ? options.IndexOf(Current) : -1;

        /// <summary>The name of the option at a place.</summary>
        string IChoice.OptionName(int index)
        {
            return options[index].Name;
        }

        /// <summary>The best score of the option at a place over the list at the last Pick.</summary>
        float IChoice.OptionScore(int index)
        {
            var option = options[index];
            return option == Current ? currentScore : option.lastScore;
        }

        /// <summary>Whether -force has the option at a place win.</summary>
        bool IChoice.IsForced(int index)
        {
            return forced == options[index];
        }

        /// <summary>-force: has the option at a place win at every Pick whatever the scores, or no longer with -1.</summary>
        void IChoice.Force(int index)
        {
            forced = index >= 0 ? options[index] : null;
        }

        /// <summary>"  Farm 2  urgency 0.80, distance 1,200.00": the current thing, then the current option's inputs with their values for it.</summary>
        string IChoice.CurrentText()
        {
            if (Current == null)
            {
                return string.Empty;
            }

            var text = "  " + BrainTools.Text(CurrentItem);
            var inputs = Current.inputs;
            for (var k = 0; k < inputs.Count && k < currentValues.Count; k++)
            {
                text = text + (k == 0 ? "  " : ", ") + inputs[k].Name + " " + BrainTools.Hundredths(currentValues[k]);
            }

            return text;
        }

        /// <summary>Notes the current pair for the log.</summary>
        void IChoice.LogNote()
        {
            loggedOption = Current;
            loggedItem = CurrentItem;
            loggedScore = currentScore;
        }

        /// <summary>"build Farm 2 0.61 (was build Farm 1 0.40)" when the current option or thing changed since it was noted, noting it anew; null when neither did.</summary>
        string? IChoice.LogChange()
        {
            if (Current == loggedOption && CurrentItem == loggedItem)
            {
                return null;
            }

            var text = Picked(Current, CurrentItem, currentScore) + " (was " + Picked(loggedOption, loggedItem, loggedScore) + ")";
            loggedOption = Current;
            loggedItem = CurrentItem;
            loggedScore = currentScore;
            return text;
        }

        /// <summary>"build Farm 2 0.61" for an option, its thing and a score; "none" for no option.</summary>
        private static string Picked(Option<T>? option, T? item, float score)
        {
            return option != null ? option.Name + " " + BrainTools.Text(item) + " " + BrainTools.Hundredths(score) : "none";
        }

        /// <summary>
        /// Lists the choice on the blackboard of its first Pick, for the AI tools' view; has a forced option win, with the
        /// thing it scored best for, else the first in the list, and none with an empty list; and keeps the current pair's
        /// input values for the view.
        /// </summary>
        private void Tool(Blackboard board, IReadOnlyList<T> items, bool kept)
        {
            if (!listed)
            {
                listed = true;
                board.AddChoice(this);
            }

            if (forced != null)
            {
                Current = items.Count > 0 ? forced : null;
                CurrentItem = items.Count > 0 ? forced.lastItem ?? items[0] : null;
                currentScore = forced.lastScore;
            }

            var from = forced != null ? forcedValues : kept ? keptValues : bestValues;
            currentValues.Clear();
            for (var i = 0; Current != null && i < from.Count; i++)
            {
                currentValues.Add(from[i]);
            }
        }
#endif
    }
}
