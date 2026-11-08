using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Random numbers that are the same on every machine. Game.Random draws from the game's own generator; new
    /// Dice(seed) is a generator of its own that gives the same numbers from the same seed in every game and in .NET.
    /// Neither may be used inside Local.For, where only one machine would draw.
    /// </summary>
    public sealed class Dice
    {
        // Park and Miller's generator with Schrage's step: no number on the way passes 2^31 - 1, so the game's 32-bit
        // Lua, desktop Lua and .NET all give the same sequence.
        private const int Modulus = 2147483647;
        private const int Multiplier = 48271;
        private const int Quotient = Modulus / Multiplier;
        private const int Remainder = Modulus % Multiplier;

        /// <summary>How many numbers one draw can give: 0 to Span - 1.</summary>
        private const int Span = Modulus - 1;

        private readonly bool seeded;
        private int state;

        /// <summary>A generator of its own: the same seed gives the same numbers, in every game and in .NET.</summary>
        public Dice(int seed)
        {
            seeded = true;
            state = seed % Span;
            if (state < 0)
            {
                state += Span;
            }

            state += 1;
        }

        private Dice()
        {
        }

        /// <summary>The game's own generator, the one GetRandomInt draws from.</summary>
        internal static Dice OfGame() => new Dice();

        /// <summary>A whole number from low to high, both included; the bounds may come in either order.</summary>
        public int Int(int low, int high)
        {
            if (high < low)
            {
                var swapped = low;
                low = high;
                high = swapped;
            }

            if (!seeded)
            {
                return Runtime.World.RandomBetween(low, high);
            }

            var count = high - low + 1;
            if (count <= 0 || count > Span)
            {
                throw new ArgumentException("Dice.Int takes at most 2147483646 numbers from low to high, not " + low + " to " + high);
            }

            // Draws that would favour the low numbers are thrown back.
            var limit = Span - Span % count;
            var drawn = Draw();
            while (drawn >= limit)
            {
                drawn = Draw();
            }

            return low + drawn % count;
        }

        /// <summary>A number from low up to high, high left out.</summary>
        public float Real(float low, float high)
        {
            return low + (high - low) * (Int(0, 16777215) / 16777216.0f);
        }

        /// <summary>True with the chance, from 0 (never) to 1 (always): Chance(0.25f) is true one time in four.</summary>
        public bool Chance(float chance)
        {
            return Real(0.0f, 1.0f) < chance;
        }

        /// <summary>An angle in degrees, from 0 up to 360.</summary>
        public float Angle()
        {
            return Real(0.0f, 360.0f);
        }

        /// <summary>One of the list's items, each as likely.</summary>
        public T Pick<T>(IReadOnlyList<T> list)
        {
            if (list.Count == 0)
            {
                throw new ArgumentException("Dice.Pick needs a list with an item in it");
            }

            return list[Int(0, list.Count - 1)];
        }

        /// <summary>Puts the list's items in a random order, in place.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Int(0, i);
                var swapped = list[i];
                list[i] = list[j];
                list[j] = swapped;
            }
        }

        /// <summary>The next number of the generator of its own, from 0 to Span - 1.</summary>
        private int Draw()
        {
            state = Multiplier * (state % Quotient) - Remainder * (state / Quotient);
            if (state <= 0)
            {
                state += Modulus;
            }

            return state - 1;
        }
    }
}
