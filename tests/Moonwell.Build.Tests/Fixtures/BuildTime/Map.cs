using Moonwell;
using Moonwell.Objects;

namespace BuildTimeFixture
{
    /// <summary>A map whose build code reads its regions, pathing and trees, and whose game code reads the results from Data.</summary>
    public sealed class BuildTimeMap : Map
    {
        public BuildTimeMap()
        {
            Name = "BuildTime";
            Components.Add(new Start());
        }
    }

    /// <summary>Game code: reads by name what the build worked out.</summary>
    public sealed class Start : GameComponent
    {
        public override void OnGameStart()
        {
            Game.Print("The shop has room for " + Data.Districts.Shop.BuildableCells + " cells of buildings.");
            Game.Print("The arena's largest footprint is " + Data.Districts.Arena.FootprintColumns + " by " + Data.Districts.Arena.FootprintRows + ".");
            if (Data.Districts.Arena.TouchesWater)
            {
                Game.Print("The arena is on the shore, " + Data.Districts.Arena.ToWater + " from the water.");
            }

            Game.Print("Trees in the arena: " + Data.Trees.Arena.Count);
            Unit.Create<Lumberjack>(Player.One, 0f, 0f, Game.Instance);
        }
    }

    /// <summary>An object class: its constructor runs on .NET in the build and reads a result of the build code's.</summary>
    [ObjectId("h900")]
    public sealed class Lumberjack : Human.Units.Peasant
    {
        public Lumberjack()
        {
            GoldCost = 50 + Data.Trees.Arena.Count;
        }
    }
}
