using System;

namespace Moonwell.AI
{
    /// <summary>
    /// Scores every option at each pass and runs the best, the first among equals; a score of 0 or less is never picked. A
    /// running option is kept unless another beats it by more than a tenth or its own score drops to 0 or less; a switch
    /// halts it. With every score at 0 or less, it halts what runs and gives Failed. It ends as its option ends.
    /// </summary>
    internal sealed class PickNode : Branch
    {
        /// <summary>How much better another option must score to take over from the running one: more than a tenth.</summary>
        private const float Switch = 1.1f;

        /// <summary>The options, as Scored made them.</summary>
        private readonly ScoredNode[] options;

        /// <summary>The running option; -1 for none.</summary>
        private int current;

        /// <summary>A pick of options made with Scored; any other node throws an ArgumentException.</summary>
        internal PickNode(Node[] options)
            : base(options)
        {
            this.options = new ScoredNode[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i] as ScoredNode;
                if (option == null)
                {
                    throw new ArgumentException("Pick takes options made with Scored, as Pick(Scored(() => score, node), ...), and option " + (i + 1) + " is not one.");
                }

                this.options[i] = option;
            }
        }

        /// <summary>No option runs yet.</summary>
        protected override void Start()
        {
            current = -1;
        }

        /// <summary>Scores the options, keeps or switches, and runs the one picked.</summary>
        protected override Status Run()
        {
            var best = -1;
            var bestScore = 0f;
            var currentScore = 0f;
            for (var i = 0; i < options.Length; i++)
            {
                var score = options[i].score();
                if (i == current)
                {
                    currentScore = score;
                }

                if (score > 0f && (best < 0 || score > bestScore))
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (current >= 0 && current != best)
            {
                if (currentScore > 0f && !(bestScore > currentScore * Switch))
                {
                    best = current;
                }
                else
                {
                    options[current].Halt();
                }
            }

            current = best;
            if (current < 0)
            {
                return Status.Failed;
            }

            var status = options[current].Tick();
            if (status != Status.Running)
            {
                current = -1;
            }

            return status;
        }
    }
}
