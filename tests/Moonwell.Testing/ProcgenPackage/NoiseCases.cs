using System;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>Cases for the procgen package's noise and fields (decision 269): numbers from a seed that are the same in .NET and in Lua.</summary>
    public static class NoiseCases
    {
        /// <summary>The hash's 24 bits for a seed and a whole point.</summary>
        private static int Bits(int seed, int x, int y)
        {
            return (int)(Noise.Hash(seed, x, y) * 16777216f);
        }

        /// <summary>A seed and a whole point give the same number in .NET and in Lua: the ones worked out outside both, with 64-bit whole numbers.</summary>
        public static void Hash(FakeWorld world)
        {
            Expect.Equal(13951142, Bits(1, 0, 0), "seed 1 at 0, 0");
            Expect.Equal(12749724, Bits(1, 1, 0), "one to the right");
            Expect.Equal(15190565, Bits(1, 0, 1), "one up");
            Expect.Equal(10258995, Bits(42, 7, -3), "a negative row");
            Expect.Equal(6459689, Bits(-5, -100, 2000), "a negative seed and column");
            Expect.Equal(4163967, Bits(2026, 123456, 654321), "a far point");
            Expect.Equal(16563313, Bits(0, 0, 0), "seed 0 at 0, 0");
            var outside = false;
            for (var k = -50; k < 50; k++)
            {
                var value = Noise.Hash(k, k * 7, -k);
                outside = outside || value < 0f || value >= 1f;
            }

            Expect.True(!outside, "from 0 up to 1");
        }

        /// <summary>At is the hash at whole points, the mean half way between two, from 0 to 1, and close points give close numbers.</summary>
        public static void Smooth(FakeWorld world)
        {
            Expect.Near(Noise.Hash(9, 3, 4), Noise.At(9, 3f, 4f), "a whole point is its hash");
            Expect.Near(Noise.Hash(9, -3, -4), Noise.At(9, -3f, -4f), "a negative one too");
            Expect.Near((Noise.Hash(9, 3, 4) + Noise.Hash(9, 4, 4)) / 2f, Noise.At(9, 3.5f, 4f), "half way is the mean");
            Expect.Near((Noise.Hash(9, -3, -4) + Noise.Hash(9, -3, -3)) / 2f, Noise.At(9, -3f, -3.5f), "below 0 too");
            var outside = false;
            var jump = 0f;
            var before = Noise.At(9, -2f, 0.3f);
            for (var k = 1; k <= 400; k++)
            {
                var value = Noise.At(9, -2f + k * 0.01f, 0.3f);
                outside = outside || value < 0f || value > 1f;
                jump = Math.Max(jump, Math.Abs(value - before));
                before = value;
            }

            Expect.True(!outside, "from 0 to 1");
            Expect.True(jump < 0.03f, "close points give close numbers: " + jump);
        }

        /// <summary>One layer is At; a second is twice as fine and as strong as the roughness; the sum is brought back to 0 to 1; no layers and a roughness below 0 throw.</summary>
        public static void Layered(FakeWorld world)
        {
            Expect.Near(Noise.At(5, 1.3f, 2.7f), Noise.Layered(5, 1.3f, 2.7f, 1, 0.5f), "one layer is At");
            Expect.Near((Noise.At(5, 1.3f, 2.7f) + 0.5f * Noise.At(1018, 2.6f, 5.4f)) / 1.5f, Noise.Layered(5, 1.3f, 2.7f, 2, 0.5f), "two layers");
            var outside = false;
            for (var k = 0; k < 200; k++)
            {
                var value = Noise.Layered(31, k * 0.37f, k * -0.21f, 5, 0.8f);
                outside = outside || value < 0f || value > 1f;
            }

            Expect.True(!outside, "from 0 to 1");
            Expect.Throws<ArgumentException>(() => Noise.Layered(5, 0f, 0f, 0, 0.5f), "no layers");
            Expect.Throws<ArgumentException>(() => Noise.Layered(5, 0f, 0f, 2, -1f), "a roughness below 0");
        }

        /// <summary>A field starts as zeros, keeps what is set, knows its lowest and highest, and refuses a cell outside it and a size below 1.</summary>
        public static void FieldBasics(FakeWorld world)
        {
            var field = new Field(3, 2);
            Expect.Equal(3, field.Columns, "columns");
            Expect.Equal(2, field.Rows, "rows");
            Expect.Near(0f, field[2, 1], "zeros");
            field[2, 1] = 7f;
            field[0, 0] = -3f;
            Expect.Near(7f, field[2, 1], "what was set");
            Expect.Near(0f, field[1, 1], "the cell beside it");
            Expect.Near(-3f, field.Lowest(), "the lowest");
            Expect.Near(7f, field.Highest(), "the highest");
            Expect.Throws<ArgumentOutOfRangeException>(() => field[3, 0] = 1f, "a column outside");
            Expect.Throws<ArgumentOutOfRangeException>(() => field[0, -1] = 1f, "a row outside");
            Expect.Throws<ArgumentException>(() => new Field(0, 2), "no columns");
            Expect.Throws<ArgumentException>(() => Field.FromNoise(1, 4, 4, 0f), "a feature size of 0");
            var noise = Field.FromNoise(77, 8, 6, 4f);
            Expect.Near(Noise.Layered(77, 5 / 4f, 3 / 4f, 4, 0.5f), noise[5, 3], "FromNoise is Layered at each cell");
        }

        /// <summary>Normalize stretches a field to 0 to 1, FadeEdges brings its edges to 0, and Smooth takes the mean of a cell and the cells that touch it.</summary>
        public static void FieldShaping(FakeWorld world)
        {
            var field = new Field(2, 2);
            field[0, 0] = 2f;
            field[1, 0] = 4f;
            field[0, 1] = 6f;
            field[1, 1] = 10f;
            field.Normalize();
            Expect.Near(0f, field[0, 0], "the lowest is 0");
            Expect.Near(0.25f, field[1, 0], "a quarter of the way");
            Expect.Near(1f, field[1, 1], "the highest is 1");
            var flat = new Field(2, 1);
            flat[0, 0] = 5f;
            flat[1, 0] = 5f;
            flat.Normalize();
            Expect.Near(0f, flat[1, 0], "a flat field becomes 0");

            var fade = new Field(7, 7);
            for (var row = 0; row < 7; row++)
            {
                for (var column = 0; column < 7; column++)
                {
                    fade[column, row] = 1f;
                }
            }

            fade.FadeEdges(0f);
            Expect.Near(1f, fade[0, 3], "a fade of 0 does nothing");
            fade.FadeEdges(2f);
            Expect.Near(0f, fade[0, 3], "the edge");
            Expect.Near(0.5f, fade[1, 3], "half way in");
            Expect.Near(1f, fade[2, 3], "as far in as the fade");
            Expect.Near(1f, fade[3, 3], "the middle");
            Expect.Near(0f, fade[6, 6], "the far corner");

            var smooth = new Field(3, 3);
            smooth[1, 1] = 9f;
            smooth.Smooth();
            Expect.Near(1f, smooth[1, 1], "the mean of nine cells");
            Expect.Near(1.5f, smooth[1, 0], "an edge cell has six");
            Expect.Near(2.25f, smooth[0, 0], "a corner cell has four");
        }
    }
}
