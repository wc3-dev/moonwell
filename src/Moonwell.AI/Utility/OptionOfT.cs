using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// Something a brain can do with a thing, for a <see cref="Choice{T}"/>, with the inputs that score it for each thing:
    /// <c>new Option&lt;Job&gt;("build", Build) { { "urgency", job =&gt; job.Urgency, 0, 10, Curve.Rising }, { "distance", job =&gt; DistanceTo(job), 0, 4096, Curve.Falling } }</c>.
    /// An input reads the thing, or a fact on the brain's blackboard, scales it from its min..max to 0..1 and puts it
    /// through its curve. The option's score for a thing is its inputs' scores multiplied and evened out over how many
    /// there are, times its weight; with no inputs, its weight, the same for every thing.
    /// </summary>
    /// <typeparam name="T">The things the option is scored for.</typeparam>
    public sealed class Option<T> : IEnumerable
        where T : class
    {
        /// <summary>The inputs, in the order added.</summary>
        internal readonly List<Input> inputs = new List<Input>();

        /// <summary>What the option does with the thing it won with, run by Run.</summary>
        private readonly Action<T> run;

        /// <summary>What the score is multiplied by.</summary>
        private float weight;

        /// <summary>The best score over the list at the last Pick of its choice; 0 before any.</summary>
        internal float lastScore;

        /// <summary>The thing that best score was for; null when none scored above 0.</summary>
        internal T? lastItem;

        /// <summary>An option with no inputs yet: add them with <c>{ thing =&gt; value, curve }</c>, <c>{ thing =&gt; value, min, max, curve }</c>, either with a name first, or a fact as on an <see cref="Option"/>. A weight below 0 throws an ArgumentException.</summary>
        /// <param name="name">The option's name, for errors and the AI tools.</param>
        /// <param name="run">What the option does with the thing it wins with, usually a method of the brain.</param>
        /// <param name="weight">What the score is multiplied by; with no inputs, the score.</param>
        public Option(string name, Action<T> run, float weight = 1f)
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

        /// <summary>An input reading a value of 0..1 from the thing, put through a curve; the AI tools name it by its place, "input 1".</summary>
        public void Add(Func<T, float> read, Curve curve)
        {
            AddInput("input " + (inputs.Count + 1), (board, item) => read(item), 0f, 1f, curve);
        }

        /// <summary>An input reading a value from the thing, scaled from min..max to 0..1, then put through a curve; the AI tools name it by its place. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(Func<T, float> read, float min, float max, Curve curve)
        {
            AddInput("input " + (inputs.Count + 1), (board, item) => read(item), min, max, curve);
        }

        /// <summary>A named input reading a value of 0..1 from the thing, put through a curve.</summary>
        public void Add(string name, Func<T, float> read, Curve curve)
        {
            AddInput(name, (board, item) => read(item), 0f, 1f, curve);
        }

        /// <summary>A named input reading a value from the thing, scaled from min..max to 0..1, then put through a curve. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(string name, Func<T, float> read, float min, float max, Curve curve)
        {
            AddInput(name, (board, item) => read(item), min, max, curve);
        }

        /// <summary>An input whose fact is 0..1 already, put through a curve: the same for every thing.</summary>
        public void Add(Fact<float> fact, Curve curve)
        {
            AddInput(fact.Name, (board, item) => board.Read(fact), 0f, 1f, curve);
        }

        /// <summary>An input whose fact is scaled from min..max to 0..1, then put through a curve: the same for every thing. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(Fact<float> fact, float min, float max, Curve curve)
        {
            AddInput(fact.Name, (board, item) => board.Read(fact), min, max, curve);
        }

        /// <summary>An input whose fact is scaled from min..max to 0..1, then put through a curve: the same for every thing. A max below the min reverses the range; a min equal to the max throws an ArgumentException.</summary>
        public void Add(Fact<int> fact, float min, float max, Curve curve)
        {
            AddInput(fact.Name, (board, item) => board.Read(fact), min, max, curve);
        }

        /// <summary>An input whose fact gives 0 for false and 1 for true, put through a curve: the same for every thing.</summary>
        public void Add(Fact<bool> fact, Curve curve)
        {
            AddInput(fact.Name, (board, item) => board.Read(fact) ? 1f : 0f, 0f, 1f, curve);
        }

        /// <summary>
        /// The option's score for a thing on a blackboard, as an <see cref="Option"/>'s: with no inputs, the weight; otherwise
        /// the inputs scored in the order added, their scores multiplied, evened out over how many there are (the n-th root),
        /// times the weight. Once an input scores 0 the score is 0 and the inputs after it are not read.
        /// </summary>
        public float Score(Blackboard board, T item)
        {
            var product = 1f;
            for (var i = 0; i < inputs.Count; i++)
            {
                var score = inputs[i].Score(board, item);
                if (score <= 0f)
                {
                    return 0f;
                }

                product *= score;
            }

            if (inputs.Count > 1)
            {
                product = (float)Math.Pow(product, 1.0 / inputs.Count);
            }

            return product * weight;
        }

        /// <summary>Runs what the option does with a thing.</summary>
        public void Run(T item)
        {
            run(item);
        }

        /// <summary>The names of the inputs, in the order added; there for the collection initializer.</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            for (var i = 0; i < inputs.Count; i++)
            {
                yield return inputs[i].Name;
            }
        }
#if DEBUG

        /// <summary>Puts the values the inputs read at the last Score into a list, for the AI tools.</summary>
        internal void ValuesInto(List<float> values)
        {
            values.Clear();
            for (var i = 0; i < inputs.Count; i++)
            {
                values.Add(inputs[i].lastValue);
            }
        }
#endif

        /// <summary>Adds an input; a min equal to the max throws an ArgumentException.</summary>
        private void AddInput(string name, Func<Blackboard, T, float> read, float min, float max, Curve curve)
        {
            if (min == max)
            {
                throw new ArgumentException("The input \"" + name + "\" of the option \"" + Name + "\" needs a min and a max that differ, not both " + min + ".");
            }

            inputs.Add(new Input(name, read, min, max, curve));
        }

        /// <summary>A value read from a thing or a fact, the range it is scaled from and the curve it is put through.</summary>
        internal sealed class Input
        {
            /// <summary>The input's name.</summary>
            internal readonly string Name;

            /// <summary>Reads the value for a thing on a blackboard.</summary>
            internal readonly Func<Blackboard, T, float> Read;

            /// <summary>The value that scales to 0.</summary>
            internal readonly float Min;

            /// <summary>The value that scales to 1.</summary>
            internal readonly float Max;

            /// <summary>What the scaled value is put through.</summary>
            internal readonly Curve Curve;
#if DEBUG

            /// <summary>The value the last Score read, for the AI tools; 0 before any.</summary>
            internal float lastValue;
#endif

            /// <summary>Makes an input.</summary>
            internal Input(string name, Func<Blackboard, T, float> read, float min, float max, Curve curve)
            {
                Name = name;
                Read = read;
                Min = min;
                Max = max;
                Curve = curve;
            }

            /// <summary>The input's score for a thing on a blackboard: the value scaled from min..max to 0..1, then put through the curve, which clamps it.</summary>
            internal float Score(Blackboard board, T item)
            {
                var value = Read(board, item);
#if DEBUG
                lastValue = value;
#endif
                return Curve.Of((value - Min) / (Max - Min));
            }
        }
    }
}
