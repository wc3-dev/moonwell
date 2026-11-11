using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for the count of nested game events: a chain more than 20 deep is reported once, with its reaction, and nothing is stopped (decision 276). The order and dummy guards have their own cases, which allow no second report.</summary>
    public static class NestingCases
    {
        /// <summary>Registers ChainLink and SecondLink.</summary>
        private static void Links()
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h0ln"), () => new ChainLink());
            ObjectTypes.Unit(ObjectTypes.FourCC("h0l2"), () => new SecondLink());
        }

        /// <summary>Makes units of Player 1 that each kill the next from OnDeath, so killing the first nests a death event for each.</summary>
        private static List<ChainLink> Chain(FakeWorld world, int length, bool second = false)
        {
            var links = new List<ChainLink>();
            for (var i = 0; i < length; i++)
            {
                var link = (ChainLink)Unit.Of(world.NewUnit(ObjectTypes.FourCC(second ? "h0l2" : "h0ln"), slot: 0))!;
                if (i > 0)
                {
                    links[i - 1].Next = link;
                }

                links.Add(link);
            }

            return links;
        }

        /// <summary>How many of the links ran their OnDeath.</summary>
        private static int Died(List<ChainLink> links)
        {
            var died = 0;
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].Died)
                {
                    died++;
                }
            }

            return died;
        }

        /// <summary>Deaths nested 25 deep are reported once, at the 21st, with the depth and the reaction; every death runs its reaction all the same.</summary>
        public static void PastTheLimit(FakeWorld world)
        {
            Links();
            var links = Chain(world, 25);
            links[0].Kill();
            Expect.Equal(25, Died(links), "every death ran");
            Expect.Equal(1, world.Printed.Count, "one line shown");
            Expect.Equal(1, world.Logged.Count, "one line logged");
            Expect.True(world.Logged[0].StartsWith("Moonwell: Moonwell.Events"), "reported: " + world.Logged[0]);
            Expect.True(world.Logged[0].Contains("more than 20 deep") && world.Logged[0].Contains("OnDeath for ChainLink"), "with the depth and the reaction: " + world.Logged[0]);
        }

        /// <summary>Deaths nested 20 deep are reported by nothing.</summary>
        public static void AtTheLimit(FakeWorld world)
        {
            Links();
            var links = Chain(world, 20);
            links[0].Kill();
            Expect.Equal(20, Died(links), "every death ran");
            Expect.Equal(0, world.Logged.Count, "nothing logged");
        }

        /// <summary>A chain's end starts the count again: the next deep chain is reported too, and one of 20 after it is not.</summary>
        public static void EachChain(FakeWorld world)
        {
            Links();
            Chain(world, 25)[0].Kill();
            Expect.Equal(1, world.Logged.Count, "the first chain");
            Chain(world, 25, second: true)[0].Kill();
            Expect.Equal(2, world.Logged.Count, "the second chain");
            Expect.True(world.Logged[1].Contains("OnDeath for SecondLink"), "with its reaction: " + world.Logged[1]);
            Chain(world, 20, second: true)[0].Kill();
            Expect.Equal(2, world.Logged.Count, "a chain of 20 after them");
        }
    }

    /// <summary>A unit that kills the next link from its OnDeath.</summary>
    public class ChainLink : Unit
    {
        /// <summary>The unit this one's death kills; null at the chain's end.</summary>
        public Unit? Next;

        /// <summary>Whether OnDeath ran.</summary>
        public bool Died;

        /// <summary>Notes the death, and kills the next link inside it.</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            Died = true;
            Next?.Kill();
        }
    }

    /// <summary>A link of another class, so its chain's report differs from ChainLink's and is shown.</summary>
    public sealed class SecondLink : ChainLink
    {
    }
}
