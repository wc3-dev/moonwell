using System.Collections.Generic;
using System.Drawing;
using Moonwell.BuildTime;

namespace BuildTimeFixture.BuildTime
{
    /// <summary>
    /// The first use: for each region of the map, how much of it can be built on, the largest footprint that fits and whether it
    /// is on the water, given to the game by the region's name.
    /// </summary>
    public sealed class Districts : BuildStep
    {
        public override void Run()
        {
            var water = Map.Cells.Where(cell => cell.IsWater);
            var districts = new Dictionary<string, object>();
            var picture = new Picture(Map).Fill(water, Color.Blue);
            foreach (var region in Map.Regions.Regions)
            {
                var buildable = Map.Cells.In(region).Where(cell => cell.CanBuild);
                var largest = buildable.LargestRectangle();
                var toWater = Map.Cells.In(region).DistanceTo(water);
                districts[region.Name] = new
                {
                    BuildableCells = buildable.Count,
                    FootprintColumns = largest.Columns,
                    FootprintRows = largest.Rows,
                    TouchesWater = toWater <= 64f,
                    ToWater = float.IsInfinity(toWater) ? -1f : toWater,
                };
                picture.Fill(buildable, Color.Green);
                if (buildable.IsEmpty)
                {
                    Warn($"region {region.Name} has no buildable ground");
                }
            }

            Give("Districts", districts);
            Report("districts.png", picture);
        }
    }
}
