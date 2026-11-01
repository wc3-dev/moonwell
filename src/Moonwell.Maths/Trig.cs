using System;

namespace Moonwell
{
    /// <summary>
    /// The one place the maths package does trigonometry and square roots, in radians. It uses Lua's math (through MathF),
    /// which gives what the natives Sin, Cos and Atan2 give at the same cost; if machines turn out to disagree on its last
    /// bit, this class switches to the natives.
    /// </summary>
    internal static class Trig
    {
        /// <summary>The sine of an angle in radians.</summary>
        public static float Sin(float radians)
        {
            return MathF.Sin(radians);
        }

        /// <summary>The cosine of an angle in radians.</summary>
        public static float Cos(float radians)
        {
            return MathF.Cos(radians);
        }

        /// <summary>The angle in radians of the direction (x, y), from -pi to pi.</summary>
        public static float Atan2(float y, float x)
        {
            return MathF.Atan2(y, x);
        }

        /// <summary>The square root, with one correction step: the game's is up to 2 parts in 100,000 high, and the native's the same, and the step leaves about two float steps.</summary>
        public static float Sqrt(float value)
        {
            var root = MathF.Sqrt(value);
            return root == 0f ? 0f : (root + value / root) / 2f;
        }
    }
}
