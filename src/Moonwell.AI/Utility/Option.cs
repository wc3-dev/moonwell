using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// Something a brain can do, for a <see cref="Choice"/>, with the inputs that score it:
    /// <c>new Option("flee", Flee) { { Life, Curve.Falling }, { EnemiesNear, 0, 6, Curve.Rising } }</c>. An input reads a
    /// fact, scales it from its min..max to 0..1 and puts it through its curve. The option's score is its inputs' scores
    /// multiplied and evened out over how many there are, times its weight; with no inputs, its weight.
    /// </summary>
    public sealed class Option : IEnumerable
    {
        /// <summary>The inputs, in the order added.</summary>
        private readonly List<Input> inputs = new List<Input>();

        /// <summary>What the option does, run by Run.</summary>
        private readonly Action run;

        /// <summary>What the score is multiplied by.</summary>
        private float weight;

        /// <summary>The score the last Score gave, for the AI tools; 0 before any.</summary>
        internal float lastScore;

        /// <summary>An option with no inputs yet: add them with <c>{ fact, curve }</c> or <c>{ fact, min, max, curve }</c>. A weight below 0 throws an ArgumentException.</summary>
        /// <param name="name">The option's name, for errors and the AI tools.</param>
        /// <param name="run">What the option does when it wins, usually a method of the brain.</param>
        /// <param name="weight">What the score is multiplied by; with no inputs, the score.</param>
        public Option(string name, Action run, float weight = 1f)
        {
            Name = name;
            this.run = run;
            Weight = weight;
        }

        /// <summary>The option's name.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>What the score is multiplied by; with no inputs, the score. Below 0 throws an ArgumentException.</summary>
        [LocalSafe]
        public float Weight
        {
            get { return weight; }
            set
            {
                if (!(value >= 0f))
                {
                    throw new ArgumentException("The weight of the option \"" + Name + "\" must be 0 or more, not " + value + ".");
                }

                weight = value;
            }
        }

        /// <summary>An input whose fact is 0..1 already, put through a curve.</summary>
        public void Add(Fact<float> fact, Curve curve)
        {
            AddInput(fact.Name, board => board.Read(fact), 0f, 1f, curve);
        }

        /// <summary>An input whose fact is scaled from min..max to 0..1, then put through a curve. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(Fact<float> fact, float min, float max, Curve curve)
        {
            AddInput(fact.Name, board => board.Read(fact), min, max, curve);
        }

        /// <summary>An input whose fact is scaled from min..max to 0..1, then put through a curve. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(Fact<int> fact, float min, float max, Curve curve)
        {
            AddInput(fact.Name, board => board.Read(fact), min, max, curve);
        }

        /// <summary>An input whose fact gives 0 for false and 1 for true, put through a curve.</summary>
        public void Add(Fact<bool> fact, Curve curve)
        {
            AddInput(fact.Name, board => board.Read(fact) ? 1f : 0f, 0f, 1f, curve);
        }

        /// <summary>
        /// The option's score on a blackboard, each fact read up its chain as <c>Blackboard.Read</c> reads it. With no inputs,
        /// the weight. Otherwise the inputs are scored in the order added, and their scores multiplied, evened out over how
        /// many there are (the n-th root), times the weight. Once an input scores 0 the score is 0 and the inputs after it are
        /// not read.
        /// </summary>
        public float Score(Blackboard board)
        {
            var product = 1f;
            for (var i = 0; i < inputs.Count; i++)
            {
                var score = inputs[i].Score(board);
                if (score <= 0f)
                {
                    lastScore = 0f;
                    return 0f;
                }

                product *= score;
            }

            if (inputs.Count > 1)
            {
                product = (float)Math.Pow(product, 1.0 / inputs.Count);
            }

            lastScore = product * weight;
            return lastScore;
        }

        /// <summary>Runs what the option does.</summary>
        public void Run()
        {
            run();
        }

        /// <summary>The names of the inputs' facts, in the order added; there for the collection initializer.</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            for (var i = 0; i < inputs.Count; i++)
            {
                yield return inputs[i].Name;
            }
        }

        /// <summary>Adds an input; a min equal to the max throws an ArgumentException.</summary>
        private void AddInput(string fact, Func<Blackboard, float> read, float min, float max, Curve curve)
        {
            if (min == max)
            {
                throw new ArgumentException("The input \"" + fact + "\" of the option \"" + Name + "\" needs a min and a max that differ, not both " + min + ".");
            }

            inputs.Add(new Input(fact, read, min, max, curve));
        }

        /// <summary>A fact read as a number, the range it is scaled from and the curve it is put through.</summary>
        internal sealed class Input
        {
            /// <summary>The fact's name.</summary>
            internal readonly string Name;

            /// <summary>Reads the fact on a blackboard as a number.</summary>
            internal readonly Func<Blackboard, float> Read;

            /// <summary>The value that scales to 0.</summary>
            internal readonly float Min;

            /// <summary>The value that scales to 1.</summary>
            internal readonly float Max;

            /// <summary>What the scaled value is put through.</summary>
            internal readonly Curve Curve;

            /// <summary>Makes an input.</summary>
            internal Input(string name, Func<Blackboard, float> read, float min, float max, Curve curve)
            {
                Name = name;
                Read = read;
                Min = min;
                Max = max;
                Curve = curve;
            }

            /// <summary>The input's score on a blackboard: the fact scaled from min..max to 0..1, then put through the curve, which clamps it.</summary>
            internal float Score(Blackboard board)
            {
                return Curve.Of((Read(board) - Min) / (Max - Min));
            }
        }
    }
}
