using System.Collections.Generic;
using System.Linq;
using Moonwell.BuildTime;
using War3Net.Common.Extensions;

namespace BuildTimeFixture.BuildTime
{
    /// <summary>
    /// The second use, a tower defense's: the build fails unless the creeps can walk from the Shop to the Arena, and the game
    /// gets the trees in each region.
    /// </summary>
    public sealed class CreepPath : BuildStep
    {
        // Summer tree wall and Ashenvale tree wall.
        private static readonly int[] TreeTypes = { "LTlt".FromRawcode(), "ATtr".FromRawcode() };

        public override void Run()
        {
            var spawn = Map.Region("Shop");
            var goal = Map.Region("Arena");
            var reached = Map.Cells.Where(cell => cell.CanWalk).FloodFrom(spawn.CenterX, spawn.CenterY);
            if (!reached.Contains(goal.CenterX, goal.CenterY))
            {
                Fail("the creeps cannot walk from the Shop to the Arena: something blocks every way between them");
            }

            var trees = new Dictionary<string, object>();
            foreach (var region in Map.Regions.Regions)
            {
                var cells = Map.Cells.In(region);
                trees[region.Name] = new { Count = Map.Doodads.Doodads.Count(doodad => TreeTypes.Contains(doodad.TypeId) && cells.Contains(doodad.Position.X, doodad.Position.Y)) };
            }

            Give("Trees", trees);
            Report("creep-path.txt", $"The creeps reach {reached.Count} cells from the Shop.\n");
        }
    }
}
