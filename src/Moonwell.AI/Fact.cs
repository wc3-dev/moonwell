using System;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// A typed key for a value on a blackboard, made once as a static readonly field: <c>new Fact&lt;int&gt;("food")</c>. A
    /// written fact is set on a blackboard and read up its chain. A worked-out fact has a read function instead, run for the
    /// blackboard a read starts on, whose value is kept there and given again until it is older than <c>keep</c> seconds.
    /// </summary>
    public sealed class Fact<T> : IHeldFact
    {
        /// <summary>The value kept for each blackboard, by its number; a lookup only.</summary>
        private readonly Dictionary<int, Kept<T>> kept = new Dictionary<int, Kept<T>>();

        /// <summary>The read function of a worked-out fact; null for a written one.</summary>
        private readonly Func<Blackboard, T>? read;

        /// <summary>The seconds a worked-out value is given again before it is worked out anew.</summary>
        private readonly float keep;

        /// <summary>A written fact, set on a blackboard with <c>Set</c>.</summary>
        /// <param name="name">The fact's name, for errors and the reader.</param>
        public Fact(string name)
        {
            Name = name;
        }

        /// <summary>A worked-out fact: <c>new Fact&lt;int&gt;("enemies near", keep: 0.25f, read: board =&gt; ...)</c>. A keep below 0 throws an ArgumentException.</summary>
        /// <param name="name">The fact's name, for errors and the reader.</param>
        /// <param name="keep">The seconds a value worked out is given again; 0 works it out once an instant.</param>
        /// <param name="read">Works the value out for the blackboard a read starts on, which gives its <c>Unit</c> and <c>Player</c>.</param>
        public Fact(string name, float keep, Func<Blackboard, T> read)
        {
            if (!(keep >= 0f))
            {
                throw new ArgumentException("The keep of the fact \"" + name + "\" must be 0 or more, not " + keep + ".");
            }

            Name = name;
            this.keep = keep;
            this.read = read;
        }

        /// <summary>The fact's name.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>A read starting on a blackboard: a worked-out fact on that one only, a written one up the chain.</summary>
        internal T ReadFrom(Blackboard board)
        {
            if (read != null)
            {
                return WorkOut(board);
            }

            for (Blackboard? on = board; on != null; on = on.Up())
            {
                if (kept.TryGetValue(on.Number, out var found))
                {
                    return found.Value;
                }
            }

            return default!;
        }

        /// <summary>The kept value while it is not older than keep; otherwise runs the read function and keeps what it gives with the time now. A blackboard whose brain has ended keeps nothing.</summary>
        private T WorkOut(Blackboard board)
        {
            var now = Minds.Now();
            if (kept.TryGetValue(board.Number, out var found))
            {
                if (now - found.Time <= keep)
                {
                    return found.Value;
                }

                found.Value = read!(board);
                found.Time = now;
                return found.Value;
            }

            var value = read!(board);
            if (!board.ended)
            {
                kept[board.Number] = new Kept<T>(value, now);
                board.Holds(this);
            }

            return value;
        }

        /// <summary>The age of the value a read starting on a blackboard finds; float.MaxValue when none. A worked-out fact is not worked out, and never looks past the blackboard.</summary>
        internal float AgeFrom(Blackboard board)
        {
            for (Blackboard? on = board; on != null; on = on.Up())
            {
                if (kept.TryGetValue(on.Number, out var found))
                {
                    return Minds.Now() - found.Time;
                }

                if (read != null)
                {
                    break;
                }
            }

            return float.MaxValue;
        }

        /// <summary>Writes the value on a blackboard with the time now; refused for a worked-out fact, and nothing on a blackboard whose brain has ended.</summary>
        internal void SetOn(Blackboard board, T value)
        {
            if (read != null)
            {
                Errors.Refuse("Blackboard.Set", "the fact \"" + Name + "\" is worked out by its read function, so it cannot be set");
                return;
            }

            if (board.ended)
            {
                return;
            }

            var now = Minds.Now();
            if (kept.TryGetValue(board.Number, out var found))
            {
                found.Value = value;
                found.Time = now;
                return;
            }

            kept[board.Number] = new Kept<T>(value, now);
            board.Holds(this);
        }

        /// <summary>Forgets the value kept for a blackboard; false when there was none.</summary>
        bool IHeldFact.Forget(int board)
        {
            return kept.Remove(board);
        }
    }
}
