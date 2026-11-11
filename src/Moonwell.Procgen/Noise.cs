using System;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Numbers from a seed and a point, the same on every machine, in every game and in .NET: a hash for whole points
    /// and smooth noise between them. Nothing here draws from a generator, so the same call always gives the same number.
    /// </summary>
    [LocalSafe]
    public static class Noise
    {
        // Whole numbers are kept from 0 to 2^31 - 1 after every sum and product: there the game's 32-bit Lua, desktop
        // Lua's 64 bits and .NET agree on every bit, and a right shift never meets a negative number.
        private const int Mask = 0x7FFFFFFF;

        /// <summary>A number from 0 up to 1 for a whole point, unrelated to its neighbours'.</summary>
        public static float Hash(int seed, int x, int y)
        {
            return (Bits(seed, x, y) >> 7) / 16777216f;
        }

        /// <summary>
        /// Smooth noise from 0 to 1 at a point, negative ones too: <c>Hash</c> at whole points, blended between them, so
        /// one bump is about 1 across. Divide the point by the size a bump should have.
        /// </summary>
        public static float At(int seed, float x, float y)
        {
            var column = Floor(x);
            var row = Floor(y);
            var tx = Fade(x - column);
            var ty = Fade(y - row);
            var low = Hash(seed, column, row);
            low += (Hash(seed, column + 1, row) - low) * tx;
            var high = Hash(seed, column, row + 1);
            high += (Hash(seed, column + 1, row + 1) - high) * tx;
            return low + (high - low) * ty;
        }

        /// <summary>
        /// Layers of <c>At</c> added, each twice as fine and <paramref name="roughness"/> times as strong as the one
        /// before, brought back to 0 to 1: more layers add finer bumps, more roughness makes them stronger. Fewer than 1
        /// layer, or a roughness below 0, throws ArgumentException.
        /// </summary>
        public static float Layered(int seed, float x, float y, int layers, float roughness)
        {
            if (layers < 1 || !(roughness >= 0f))
            {
                throw new ArgumentException("Noise.Layered takes 1 layer or more and a roughness of 0 or more, not " + layers + " and " + roughness);
            }

            var sum = 0f;
            var total = 0f;
            var strength = 1f;
            var scale = 1f;
            for (var layer = 0; layer < layers; layer++)
            {
                sum += At(seed + layer * 1013, x * scale, y * scale) * strength;
                total += strength;
                strength *= roughness;
                scale *= 2f;
            }

            return sum / total;
        }

        /// <summary>The whole number at or below a value, a negative one too.</summary>
        internal static int Floor(float value)
        {
            var whole = (int)value;
            return whole > value ? whole - 1 : whole;
        }

        /// <summary>A blend from 0 to 1 that starts and ends level, so noise has no creases at whole points.</summary>
        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>31 scrambled bits for a seed and a whole point.</summary>
        private static int Bits(int seed, int x, int y)
        {
            var bits = Mix((seed & Mask) ^ 0x2545F491);
            bits = Mix(bits ^ (x & Mask));
            return Mix(bits ^ (y & Mask));
        }

        /// <summary>Scrambles 31 bits, so that numbers one apart come out far apart.</summary>
        private static int Mix(int bits)
        {
            bits = ((bits ^ (bits >> 15)) * 0x2C1B3C6D) & Mask;
            bits = ((bits ^ (bits >> 12)) * 0x297A2D39) & Mask;
            return bits ^ (bits >> 15);
        }
    }
}
