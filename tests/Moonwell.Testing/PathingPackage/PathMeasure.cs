using System.Text;

namespace Moonwell.Testing
{
    /// <summary>
    /// Long routes for timing the path search (backlog J173: one route down a lane about 5,000 across took 84 ms in the
    /// game). A host installs the world once with Setup, then times Route; the route comes back as its points, so a change
    /// to the search can be checked to give the same routes. The ground is a flat 6,144 square from (-3072, -3072), playable
    /// from -3000 to 3000, with a wall two cells thick at x 0 to 64 from y -512 to the top edge, which only the third route meets.
    /// Not a case: PathSpeedTests in Moonwell.Lua.Tests times it in Lua and in .NET.
    /// </summary>
    public static class PathMeasure
    {
        /// <summary>The routes measured, each from x1, y1 to x2, y2 for a collision size.</summary>
        private static readonly float[][] Routes =
        {
            new[] { 31f, -2500f, -1000f, 2500f, -1000f },
            new[] { 31f, -2500f, -2500f, 2500f, 500f },
            new[] { 31f, -2500f, 0f, 2500f, 0f },
            new[] { 48f, -2500f, -2600f, 2500f, -2600f },
        };

        private static FakeTerrainWorld? terrain;

        /// <summary>How many cells the item was put in since Setup: in the game each is a call to it, the cost of a cold search.</summary>
        public static int Asked => terrain!.PlaceCalls;

        /// <summary>How many routes there are.</summary>
        public static int Count => Routes.Length;

        /// <summary>What each route is, for the report.</summary>
        public static string Name(int route)
        {
            return route switch
            {
                0 => "straight 5,000 across open ground, size 31",
                1 => "slanting 5,831 across open ground, size 31",
                2 => "5,000 round the end of a wall 3,500 long, size 31",
                _ => "straight 5,000 across open ground, size 48",
            };
        }

        /// <summary>Installs a fresh world with the ground; the cells' item answers start empty, as at a map's first search.</summary>
        public static void Setup()
        {
            FakeWorld.Install();
            terrain = new FakeTerrainWorld { AreaMinX = -3000f, AreaMinY = -3000f, AreaMaxX = 3000f, AreaMaxY = 3000f };
            const int corners = 49;
            var text = new StringBuilder();
            for (var k = 0; k < corners * corners; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(terrain, corners, corners, -3072f, -3072f, text.ToString());
            for (var row = -16; row <= 93; row++)
            {
                terrain.Block(16f, row * 32f + 16f, FakeTerrainWorld.NoWalk);
                terrain.Block(48f, row * 32f + 16f, FakeTerrainWorld.NoWalk);
            }

            Paths.Install(new FakePathingWorld(terrain));
        }

        /// <summary>Finds a route and gives its points as whole numbers, "x,y" with spaces between, and whether it reaches.</summary>
        public static string Route(int route)
        {
            var r = Routes[route];
            var path = Paths.FindPath(r[0], r[1], r[2], r[3], r[4]);
            var text = new StringBuilder(path.Reaches ? "reaches" : "short");
            foreach (var point in path.Points)
            {
                text.Append(' ').Append((int)point.X).Append(',').Append((int)point.Y);
            }

            return text.ToString();
        }
    }
}
