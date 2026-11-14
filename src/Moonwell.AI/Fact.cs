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
        // The values and their times are two dictionaries of System types, not one of a Moonwell type: a generic class's
        // body runs when a file makes Fact<X>, which can be before this file's own imports are filled (K11).

        /// <summary>The value kept for each blackboard, by its number; a lookup only.</summary>
        private readonly Dictionary<int, T> values = new Dictionary<int, T>();

        /// <summary>The time each value was written or worked out, by the blackboard's number; a lookup only.</summary>
        private readonly Dictionary<int, float> times = new Dictionary<int, float>();

        /// <summary>The read function of a worked-out fact; null for a written one.</summary>
        private readonly Func<Blackboard, T>? read;

        /// <summary>The seconds a worked-out value is given again before it is worked out anew.</summary>
        private readonly float keep;

        /// <summary>A written fact, set on a blackboard with <c>Set</c>.</summary>
        /// <param name="name">The fact's name, for errors and the reader.</param>
        public Fact(string name)
        {
            Name = name;
#if DEBUG
            BrainTools.Made(this);
#endif
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
#if DEBUG
            BrainTools.Made(this);
#endif
        }

        /// <summary>The fact's name.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>A read starting on a blackboard: a worked-out fact on that one only, a written one up the chain.</summary>
        internal T ReadFrom(Blackboard board)
        {
#if DEBUG
            if (BrainLog.Reading)
            {
                return Recorded(board);
            }
#endif
            if (read != null)
            {
                return WorkOut(board);
            }

            for (Blackboard? on = board; on != null; on = on.Up())
            {
                if (values.TryGetValue(on.Number, out var found))
                {
                    return found;
                }
            }

            return default!;
        }

        /// <summary>The kept value while it is not older than keep; otherwise runs the read function and keeps what it gives with the time now. A blackboard whose brain has ended keeps nothing.</summary>
        private T WorkOut(Blackboard board)
        {
            var now = Minds.Now();
            if (times.TryGetValue(board.Number, out var time))
            {
                if (now - time <= keep)
                {
                    return values[board.Number];
                }

                var fresh = read!(board);
                values[board.Number] = fresh;
                times[board.Number] = now;
                return fresh;
            }

            var value = read!(board);
            if (!board.ended)
            {
                values[board.Number] = value;
                times[board.Number] = now;
                board.Holds(this);
            }

            return value;
        }

        /// <summary>The age of the value a read starting on a blackboard finds; float.MaxValue when none. A worked-out fact is not worked out, and never looks past the blackboard.</summary>
        internal float AgeFrom(Blackboard board)
        {
            for (Blackboard? on = board; on != null; on = on.Up())
            {
                if (times.TryGetValue(on.Number, out var time))
                {
                    return Minds.Now() - time;
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

            var held = times.ContainsKey(board.Number);
            values[board.Number] = value;
            times[board.Number] = Minds.Now();
            if (!held)
            {
                board.Holds(this);
            }
        }

        /// <summary>Forgets the value kept for a blackboard; false when there was none.</summary>
        bool IHeldFact.Forget(int board)
        {
            times.Remove(board);
            return values.Remove(board);
        }
#if DEBUG

        /// <summary>The fact's name, for the AI tools.</summary>
        string IHeldFact.FactName()
        {
            return Name;
        }

        /// <summary>The value kept for a blackboard as the AI tools show it; "none" when there is none.</summary>
        string IHeldFact.TextOn(int board)
        {
            return values.TryGetValue(board, out var value) ? Text(value) : "none";
        }

        /// <summary>The time the value kept for a blackboard was written or worked out; float.MaxValue when there is none.</summary>
        float IHeldFact.TimeOn(int board)
        {
            return times.TryGetValue(board, out var time) ? time : float.MaxValue;
        }

        /// <summary>-fact: sets the value a chat text gives, read by T (int, float, bool or string), on a blackboard; empty when set, otherwise why not.</summary>
        string IHeldFact.SetFromText(Blackboard board, string text)
        {
            if (read != null)
            {
                return Name + " is worked out by its read function, so it can't be set";
            }

            if (typeof(T) == typeof(string))
            {
                SetOn(board, (T)(object)text);
                return string.Empty;
            }

            if (typeof(T) == typeof(bool))
            {
                var truth = text.ToLower();
                if (truth != "true" && truth != "false" && truth != "1" && truth != "0")
                {
                    return Name + " holds a bool: true, false, 1 or 0";
                }

                SetOn(board, (T)(object)(truth == "true" || truth == "1"));
                return string.Empty;
            }

            if (typeof(T) != typeof(int) && typeof(T) != typeof(float))
            {
                return Name + " holds " + typeof(T).Name + ", and -fact sets int, float, bool and string facts";
            }

            if (!BrainTools.Number(text, out var number))
            {
                return Name + " holds a number: -fact " + Name + " 3";
            }

            if (typeof(T) == typeof(float))
            {
                SetOn(board, (T)(object)number);
                return string.Empty;
            }

            if (number < -2000000000f || number > 2000000000f || number != (int)number)
            {
                return Name + " holds a whole number";
            }

            SetOn(board, (T)(object)(int)number);
            return string.Empty;
        }

        /// <summary>A read during a logged think: read with the recording off, so a read function's own reads are not noted, then noted with its age.</summary>
        private T Recorded(Blackboard board)
        {
            BrainLog.Reading = false;
            var value = ReadFrom(board);
            BrainLog.Reading = true;
            BrainLog.Read(Name, Text(value), AgeFrom(board));
            return value;
        }

        /// <summary>
        /// A value as the AI tools show it: "none" for null, a float to hundredths, an int with commas, a bool as true or false
        /// (nullable ones too), an enum by its number, the rest as BrainTools.Text shows it. T is tested, not the value, as in
        /// Lua a number passes for any number type; CSharp.lua writes an enum type argument as int, so an enum shows as the
        /// number Lua has, in .NET too.
        /// </summary>
        private static string Text(T value)
        {
            object? boxed = value;
            if (boxed == null)
            {
                return "none";
            }

            if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
            {
                return boxed is float number ? BrainTools.Hundredths(number) : "out of range";
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                return DebugPanel.Count((int)boxed);
            }

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                return (bool)boxed ? "true" : "false";
            }

            if (boxed is Enum)
            {
                return DebugPanel.Count(Convert.ToInt32(boxed));
            }

            return BrainTools.Text(boxed);
        }
#endif
    }
}
