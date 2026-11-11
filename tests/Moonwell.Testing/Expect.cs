using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Checks a condition inside a case; a failed check throws ExpectationFailed.</summary>
    public static class Expect
    {
        /// <summary>Where failed checks go in place of being thrown, when set: the twin run sets it for a case's second machine, where the case's checks of what one player sees do not hold and the case must still run to its end.</summary>
        public static List<string>? Kept;

        /// <summary>Fails unless the two ints are equal.</summary>
        public static void Equal(int expected, int actual, string what)
        {
            if (expected != actual)
            {
                Mismatch(what, expected, actual);
            }
        }

        /// <summary>Fails unless the two strings are equal, where null equals null.</summary>
        public static void Equal(string? expected, string? actual, string what)
        {
            if (expected != actual)
            {
                Mismatch(what, expected, actual);
            }
        }

        /// <summary>Fails unless the two bools are equal.</summary>
        public static void Equal(bool expected, bool actual, string what)
        {
            if (expected != actual)
            {
                Mismatch(what, expected, actual);
            }
        }

        /// <summary>Fails unless both are the same object.</summary>
        public static void Same(object? expected, object? actual, string what)
        {
            if (!object.ReferenceEquals(expected, actual))
            {
                Mismatch(what, expected, actual);
            }
        }

        /// <summary>Fails unless the two floats are within 0.001; in the game's own Lua, whose square roots and trig are off by up to about 2 parts in 100,000, within 0.001 and 2 parts in 10,000 of the expected value.</summary>
        public static void Near(float expected, float actual, string what)
        {
            var tolerance = GameNumbers.HasNaN ? 0.001f : 0.001f + Math.Abs(expected) * 0.0002f;
            // Written so that NaN, which compares false with everything, fails.
            if (!(Math.Abs(expected - actual) <= tolerance))
            {
                Mismatch(what, expected, actual);
            }
        }

        /// <summary>Fails unless the condition holds.</summary>
        public static void True(bool condition, string what)
        {
            if (!condition)
            {
                Fail(what);
            }
        }

        /// <summary>Fails unless the action throws a TException.</summary>
        public static void Throws<TException>(Action action, string what)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                if (error is TException)
                {
                    return;
                }

                Fail(what + ": threw " + error.Message);
                return;
            }

            Fail(what + ": nothing was thrown");
        }

        private static void Mismatch(string what, object? expected, object? actual)
        {
            Fail(what + ": expected " + expected + ", got " + actual);
        }

        private static void Fail(string message)
        {
            if (Kept != null)
            {
                Kept.Add(message);
                return;
            }

            throw new ExpectationFailed(message);
        }
    }
}
