using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Moonwell.Engine;
using Moonwell.Testing;

namespace Moonwell.Tests;

/// <summary>
/// The path search's band shortcut (J173: <c>Blocked</c> and <c>BandFits</c>) keeps the turns that walking every line from a
/// turn with <c>Clear</c> gives (J181), on seeded random mazes of blocked cells and walls, for each square from one cell to
/// four, so on routes with many turns, where <c>PathSpeedTests</c>' four routes have one at most.
/// </summary>
public class PathBandTests
{
    private const int Mazes = 40;
    private const int RoutesPerMaze = 12;

    [Fact]
    public void The_band_keeps_the_turns_of_walking_every_line_on_random_mazes()
    {
        var routes = 0;
        var turns = 0;
        for (var seed = 1; seed <= Mazes; seed++)
        {
            var random = new Random(seed);
            Maze(random);
            for (var k = 0; k < RoutesPerMaze; k++)
            {
                var cells = 1 + random.Next(4);
                var fromX = Coordinate(random);
                var fromY = Coordinate(random);
                var toX = Coordinate(random);
                var toY = Coordinate(random);
                var band = Search.Find(cells, Movement.Ground, fromX, fromY, toX, toY);
                var walked = Search.Find(cells, Movement.Ground, fromX, fromY, toX, toY, true);
                var where = $"maze {seed}, route {k}: {cells} cells from {fromX},{fromY} to {toX},{toY}";
                Assert.True(Text(walked) == Text(band), $"{where}; walked: {Text(walked)}; band: {Text(band)}");
                routes++;
                turns += band.Points.Count - 2;
            }
        }

        Assert.Equal(Mazes * RoutesPerMaze, routes);

        // The mazes give routes with many turns, or the comparison shows little.
        Assert.True(turns >= 1000, $"{turns} turns in {routes} routes");
    }

    // A fresh world: a flat 2,048 square from (-1024, -1024), playable from -1000 to 1000, with some cells blocked at random and
    // a few walls, straight or slanting, each a line of blocked cells.
    private static void Maze(Random random)
    {
        FakeWorld.Install();
        var terrain = new FakeTerrainWorld { AreaMinX = -1000f, AreaMinY = -1000f, AreaMaxX = 1000f, AreaMaxY = 1000f };
        const int corners = 17;
        var text = new StringBuilder();
        for (var k = 0; k < corners * corners; k++)
        {
            text.Append(GridText.Corner(0f, 2));
        }

        Terrain.Install(terrain, corners, corners, -1024f, -1024f, text.ToString());
        var density = 0.02 + random.NextDouble() * 0.12;
        for (var col = -32; col < 32; col++)
        {
            for (var row = -32; row < 32; row++)
            {
                if (random.NextDouble() < density)
                {
                    terrain.Block(col * 32f + 16f, row * 32f + 16f, FakeTerrainWorld.NoWalk);
                }
            }
        }

        var walls = 3 + random.Next(6);
        for (var w = 0; w < walls; w++)
        {
            var col = random.Next(-32, 32);
            var row = random.Next(-32, 32);
            var stepCol = random.Next(-1, 2);
            var stepRow = stepCol == 0 ? (random.Next(2) == 0 ? -1 : 1) : random.Next(-1, 2);
            var length = 8 + random.Next(30);
            for (var k = 0; k < length; k++)
            {
                terrain.Block(col * 32f + 16f, row * 32f + 16f, FakeTerrainWorld.NoWalk);
                col += stepCol;
                row += stepRow;
            }
        }

        Paths.Install(new FakePathingWorld(terrain));
    }

    // A point in the playable area, not on a cell's edge.
    private static float Coordinate(Random random)
    {
        return random.Next(-960, 960) + 0.5f;
    }

    // A route's points exactly, and whether it reaches.
    private static string Text(Route route)
    {
        var text = new StringBuilder(route.Reaches ? "reaches" : "short");
        foreach (var point in route.Points)
        {
            text.Append(' ').Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(point.Y.ToString("R", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }
}
