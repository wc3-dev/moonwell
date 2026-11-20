using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for Dice: Game.Random on the game's generator, and a seeded generator of its own that gives the same numbers in .NET and in Lua (decision 217).</summary>
    public static class DiceCases
    {
        /// <summary>A seed gives the same numbers in .NET and in Lua: the ones worked out by hand from Park and Miller's generator.</summary>
        public static void SeededSequence(FakeWorld world)
        {
            Expect.Equal("52 0 22 4 35 92 88 15", Draws(new Dice(42), 8, 0, 99), "seed 42");
            Expect.Equal("1 6 6 1 1 5 5 5", Draws(new Dice(0), 8, 1, 6), "seed 0");
            Expect.Equal("2 1 4 6", Draws(new Dice(-5), 4, 1, 6), "a negative seed");
            Expect.Equal(Draws(new Dice(7), 20, 1, 1000), Draws(new Dice(7), 20, 1, 1000), "the same seed twice");
        }

        /// <summary>Int gives both bounds and nothing outside them, in either order; one number gives itself.</summary>
        public static void IntBounds(FakeWorld world)
        {
            var dice = new Dice(2026);
            var low = false;
            var high = false;
            var outside = false;
            for (var i = 0; i < 1000; i++)
            {
                var drawn = dice.Int(9, 3);
                low = low || drawn == 3;
                high = high || drawn == 9;
                outside = outside || drawn < 3 || drawn > 9;
            }

            Expect.Equal(true, low && high, "both bounds drawn");
            Expect.Equal(false, outside, "nothing outside them");
            Expect.Equal(5, dice.Int(5, 5), "one number");
            Expect.Throws<System.ArgumentException>(() => dice.Int(-2000000000, 2000000000), "a span past the generator's");
        }

        /// <summary>Real stays from low up to high, Angle from 0 up to 360, and Chance(0) and Chance(1) are never and always.</summary>
        public static void RealAndChance(FakeWorld world)
        {
            var dice = new Dice(11);
            var outside = false;
            var never = false;
            var always = true;
            for (var i = 0; i < 500; i++)
            {
                var real = dice.Real(2.0f, 4.0f);
                var angle = dice.Angle();
                outside = outside || real < 2.0f || real >= 4.0f || angle < 0.0f || angle >= 360.0f;
                never = never || dice.Chance(0.0f);
                always = always && dice.Chance(1.0f);
            }

            Expect.Equal(false, outside, "within the bounds");
            Expect.Equal(false, never, "Chance(0)");
            Expect.Equal(true, always, "Chance(1)");
        }

        /// <summary>Real leaves high out at the top draw too, where the sum would round up to high in 32-bit floats; the draws after it are the same as before (J143).</summary>
        public static void RealTopDraw(FakeWorld world)
        {
            // Seed 1071806939's first draw is the top one, 16777215.
            Expect.Equal(true, new Dice(1071806939).Real(1.0f, 2.0f) < 2.0f, "Real(1, 2)");
            Expect.Equal(true, new Dice(1071806939).Real(10.0f, 20.0f) < 20.0f, "Real(10, 20)");
            Expect.Equal(true, new Dice(1071806939).Real(1000.0f, 1001.0f) < 1001.0f, "Real(1000, 1001)");
            Expect.Equal(true, new Dice(1071806939).Real(-1001.0f, -1000.0f) < -1000.0f, "Real(-1001, -1000)");
            Expect.Equal(true, new Dice(1071806939).Real(1001.0f, 1000.0f) > 1000.0f, "Real(1001, 1000), the bounds the other way");
            Expect.True(new Dice(1071806939).Real(5.0f, 5.0f) == 5.0f, "Real(5, 5)");
            var dice = new Dice(1071806939);
            dice.Real(1000.0f, 1001.0f);
            var after = new Dice(1071806939);
            after.Int(0, 16777215);
            Expect.Equal(after.Int(0, 999), dice.Int(0, 999), "the next draw");
            Expect.True(1000.0f + new Dice(42).Int(0, 16777215) / 16777216.0f == new Dice(42).Real(1000.0f, 1001.0f), "a draw below the top, as before");
        }

        /// <summary>Shuffle keeps every item and Pick gives one of them; an empty list cannot be picked from.</summary>
        public static void PickAndShuffle(FakeWorld world)
        {
            var dice = new Dice(5);
            var list = new List<int> { 1, 2, 3, 4, 5, 6 };
            dice.Shuffle(list);
            var sum = 0;
            foreach (var item in list)
            {
                sum += item;
            }

            Expect.Equal(6, list.Count, "the count kept");
            Expect.Equal(21, sum, "every item kept");
            var picked = dice.Pick(new[] { "a", "b" });
            Expect.Equal(true, picked == "a" || picked == "b", "one of the items");
            Expect.Throws<System.ArgumentException>(() => dice.Pick(new List<int>()), "an empty list");
        }

        /// <summary>Game.Random asks the game's generator, with the bounds low first.</summary>
        public static void GameGenerator(FakeWorld world)
        {
            var asked = "";
            world.Pick = (low, high) =>
            {
                asked = asked + low + "-" + high + " ";
                return high;
            };

            Expect.Equal(6, Game.Random.Int(1, 6), "the game's number");
            Expect.Equal(6, Game.Random.Int(6, 1), "bounds in the other order");
            Expect.Equal("b", Game.Random.Pick(new[] { "a", "b" }), "a pick");
            Expect.Equal("1-6 1-6 0-1 ", asked, "what the game was asked");
        }

        private static string Draws(Dice dice, int count, int low, int high)
        {
            var text = "";
            for (var i = 0; i < count; i++)
            {
                text = text + (i == 0 ? "" : " ") + dice.Int(low, high);
            }

            return text;
        }
    }
}
